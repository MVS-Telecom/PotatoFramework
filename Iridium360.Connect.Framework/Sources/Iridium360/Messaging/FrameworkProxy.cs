﻿using Iridium360.Connect.Framework.Helpers;
using Iridium360.Connect.Framework.Messaging;
using Iridium360.Connect.Framework.Messaging.Legacy;
using Iridium360.Connect.Framework.Messaging.Storage;
using Iridium360.Connect.Framework.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Iridium360.Connect.Framework.Messaging
{
    /// <summary>
    /// 
    /// </summary>
    public class MessageTransmittedEventArgs : EventArgs
    {
        public string MessageId { get; set; }
    }


    /// <summary>
    /// 
    /// </summary>
    public class MessageProgressChangedEventArgs : EventArgs
    {
        public string MessageId { get; set; }
        public uint ReadyParts { get; set; }
        public uint TotalParts { get; set; }
        public double Progress => 100d * (ReadyParts / (double)TotalParts);
    }


    /// <summary>
    /// 
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        public Message Message { get; set; }
    }



    /// <summary>
    /// 
    /// </summary>
    public interface IFrameworkProxy : IFramework
    {
        event EventHandler<MessageTransmittedEventArgs> MessageTransmitted;
        event EventHandler<MessageTransmittedEventArgs> MessagePartsResending;
        event EventHandler<MessageReceivedEventArgs> MessageReceived;
        event EventHandler<MessageProgressChangedEventArgs> MessageProgressChanged;

        Task<(string messageId, int totalParts)> SendMessage(Message message, Action<double> progress = null);
    }




    /// <summary>
    /// 
    /// </summary>
    public class FrameworkProxy : IFrameworkProxy
    {
        event EventHandler<DeviceSearchResultsEventArgs> IFramework.DeviceSearchResults
        {
            add => framework.DeviceSearchResults += value;
            remove => framework.DeviceSearchResults -= value;
        }

        event EventHandler<EventArgs> IFramework.SearchTimeout
        {
            add => framework.SearchTimeout += value;
            remove => framework.SearchTimeout -= value;
        }

        event EventHandler<PacketStatusUpdatedEventArgs> IFramework.PacketStatusUpdated
        {
            add => throw new NotSupportedException($"Use `{nameof(MessageTransmitted)}` event instead");
            remove => throw new NotSupportedException($"Use `{nameof(MessageTransmitted)}` event instead");
        }

        event EventHandler<PacketReceivedEventArgs> IFramework.PacketReceived
        {
            add => throw new NotSupportedException($"Use `{nameof(MessageReceived)}` event instead");
            remove => throw new NotSupportedException($"Use `{nameof(MessageReceived)}` event instead");
        }


        public event EventHandler<MessageTransmittedEventArgs> MessageTransmitted = delegate { };
        public event EventHandler<MessageTransmittedEventArgs> MessagePartsResending = delegate { };
        public event EventHandler<MessageProgressChangedEventArgs> MessageProgressChanged = delegate { };
        public event EventHandler<MessageReceivedEventArgs> MessageReceived = delegate { };


        private IFramework framework;
        private ILogger logger;
        private IPacketBuffer buffer;
        private IStorage storage;


        public IDevice ConnectedDevice => framework.ConnectedDevice;



        public FrameworkProxy(IFramework framework, ILogger logger, IPacketBuffer buffer, IStorage storage)
        {
            this.framework = framework;
            this.logger = logger;
            this.buffer = buffer;
            this.storage = storage;

            framework.PacketStatusUpdated += Framework__PacketStatusUpdated;
            framework.PacketReceived += Framework__PacketReceived;
        }





        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Framework__PacketReceived(object sender, PacketReceivedEventArgs e)
        {
            logger.Log($"[MESSAGE] Packet received -> `0x{e.Payload.ToHexString()}`");

            try
            {
                if (Legacy_MessageMT.CheckSignature(e.Payload))
                {
                    Debugger.Break();

                    var legacy = Legacy_MessageMT.Unpack(e.Payload);

                    if (legacy.Complete)
                    {
                        Debugger.Break();

                        logger.Log($"[MESSAGE] Legacy message received Group={legacy.Group} Index={legacy.Index} Progress={legacy.ReadyParts}/{legacy.TotalParts} -> COMPLETED");

                        var subscriber = legacy.GetSubscriber();
                        var text = legacy.GetText();

                        var message = ChatMessageMT.Create(subscriber, null, null, text);

                        MessageReceived(this, new MessageReceivedEventArgs()
                        {
                            Message = message
                        });
                    }
                    else
                    {
                        logger.Log($"[MESSAGE] Legacy message received Group={legacy.Group} Index={legacy.Index} Progress={legacy.ReadyParts}/{legacy.TotalParts} -> INCOMPLETE - waiting for next parts");
                        Debugger.Break();
                    }
                }
                else if (Framework.Messaging.Message.CheckSignature(e.Payload))
                {
                    Debugger.Break();

                    var message = Message.Unpack(e.Payload);

                    if (message.Complete)
                    {
                        Debugger.Break();

                        logger.Log($"[MESSAGE] Message received Group={message.Group} Index={message.Index} Progress={message.ReadyParts}/{message.TotalParts} -> COMPLETED");

                        if (message is ResendMessagePartsMT resendMessage)
                        {
                            Debugger.Break();
                            ResendParts(resendMessage.ResendGroup, resendMessage.ResendIndexes);
                        }
                        else
                        {
                            MessageReceived(this, new MessageReceivedEventArgs()
                            {
                                Message = message
                            });
                        }
                    }
                    else
                    {
                        logger.Log($"[MESSAGE] Message received Group={message.Group} Index={message.Index} Progress={message.ReadyParts}/{message.TotalParts} -> INCOMPLETE - waiting for next parts");
                        Debugger.Break();
                    }
                }
                else
                {
                    Debugger.Break();
                    throw new NotImplementedException("Unknown bytes");
                }
            }
            catch (Exception ex)
            {
                Debugger.Break();
                logger.Log($"[MESSAGE] Exception occured while parsing `{e.Payload.ToHexString()}` {ex}");
            }


            Debugger.Break();
            e.Handled = true;
        }



        /// <summary>
        /// 
        /// </summary>
        private async void ResendParts(byte group, byte[] indexes)
        {
            var message = buffer.GetMessageByGroup(group, PacketDirection.Outbound);
            var packets = buffer.GetPackets((uint)group, PacketDirection.Outbound);
            var targets = packets.Where(x => indexes.Contains((byte)x.Index)).ToList();

            logger.Log($"[RESENDING PACKETS] {string.Join(", ", targets.Select(x => x.FrameworkId))}");

            foreach (var target in targets)
                buffer.SetPacketNotTransmitted(target.FrameworkId);

            await SendPackets(targets);


            MessagePartsResending(this, new MessageTransmittedEventArgs()
            {
                MessageId = message.Id
            });

            MessageProgressChanged(this, new MessageProgressChangedEventArgs()
            {
                MessageId = message.Id,
                ReadyParts = (uint)(packets.Count - targets.Count),
                TotalParts = message.TotalParts,
            });
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Framework__PacketStatusUpdated(object sender, PacketStatusUpdatedEventArgs e)
        {
            try
            {
                var packet = buffer.GetPacket(e.MessageId);

                if (packet == null)
                {
#if DEBUG
                    if (e.Status != MessageStatus.ReceivedByDevice)
                        Debugger.Break();
#endif
                    e.Handled = true;
                    return;
                }


                switch (e.Status)
                {
                    case MessageStatus.ReceivedByDevice:
                        break;

                    case MessageStatus.Transmitted:
                        Debugger.Break();

                        buffer.SetPacketTransmitted(e.MessageId);
                        logger.Log($"[PACKET] `{e.MessageId}` -> Transmitted");
                        break;

                    default:
                        Debugger.Break();

                        logger.Log($"[PACKET] `{e.MessageId}` -> {e.Status}");
                        ///Что-то нехорошее
                        Debugger.Break();
                        break;
                }



                if (e.Status == MessageStatus.Transmitted)
                {
                    var message = buffer.GetMessageByGroup(packet.Group, packet.Direction);

                    if (message == null)
                    {
                        logger.Log($"Message with group `{packet.Group}` not found");
                        Debugger.Break();
                        e.Handled = true;
                        return;
                    }


                    ///Кол-во отправленных чатей сообщения
                    var transmittedCount = buffer
                        .GetPackets(packet.Group, packet.Direction)
                        .Where(x => x.Status >= PacketStatus.Transmitted)
                        .Count();

                    double progress = transmittedCount / (double)packet.TotalParts;

                    logger.Log($"[MESSAGE] Message progress changed -> {Math.Round(100 * progress, 1)}% ({transmittedCount}/{packet.TotalParts})");


                    MessageProgressChanged(this, new MessageProgressChangedEventArgs()
                    {
                        MessageId = message.Id,
                        ReadyParts = (uint)transmittedCount,
                        TotalParts = packet.TotalParts
                    });


                    ///Все части отправлены == сообщение передано
                    if (transmittedCount == packet.TotalParts)
                    {
                        logger.Log($"[MESSAGE] `{message.Id}` ({message.Type}) -> Transmitted");
                        Debugger.Break();

                        MessageTransmitted(this, new MessageTransmittedEventArgs()
                        {
                            MessageId = message.Id
                        });

                        ///Удаляем пакеты -> они отправлены и больше не нужны
                        //buffer.DeletePackets(packet.Group, packet.Direction);

                        ///TODO:
                        //buffer.DeleteMessage



                        ///TODO: А если сообщение состояло из одной части о она потерялась?
                        if (packet.TotalParts > 1)
                        {
                            Debugger.Break();

                            ///Отправляем подтверждение того что все сообщение ушло
                            Task.Run(async () =>
                            {
                                logger.Log("[MESSAGE SENT] ~~~~~~~~~~~~~~~~~~~~~~");

                                var sent = MessageSentMO.Create((byte)packet.Group);
                                var result2 = await SendMessage(sent);

                                logger.Log("[MESSAGE SENT] ~~~~~~~~~~~~~~~~~~~~~~");
                                Debugger.Break();
                            });
                        }
                    }
                    else
                    {
                        ///Не все пакеты сообщения отправлены
                    }
                }

                e.Handled = true;

            }
            catch (Exception ex)
            {
                logger.Log(ex);
                Debugger.Break();

#if DEBUG
                e.Handled = false;
#else
                e.Handled = false;
#endif
            }
        }


        private static SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<(string messageId, int totalParts)> SendMessage(Message message, Action<double> progress = null)
        {
            await sendLock.WaitAsync();

            return await Task.Run(async () =>
            {
                await Reconnect();


                var messageId = ShortGuid.NewGuid().ToString();
                var group = (byte)storage.GetShort("r7-group-id", 1);


                try
                {
                    ///Т.к мы ограничены максимальным кол-вом частей == byte.max - делаем ротэйт
                    buffer.DeleteMessage(group);
                    buffer.DeletePackets(group, PacketDirection.Outbound);


                    var packets = message.Pack(group);
                    var bytes = ByteArrayHelper.Merge(packets.Select(x => x.Payload).ToList());

                    Console.WriteLine($"0x{bytes.ToHexString()}");

                    logger.Log($"[MESSAGE] Sending message Parts=`{packets.Count}` Type=`{message.GetType().Name}` Text=`{(message as ChatMessageMO)?.Text}` Location=`{(message as MessageWithLocation)?.Lat}, {(message as MessageWithLocation)?.Lon}`");


                    foreach (var packet in packets)
                        logger.Log($"   => 0x{packet.Payload.ToHexString()}");


                    if (packets.Count > 1)
                        progress?.Invoke(0);



                    ///Сразу увеличиваем - если будет ошибка, то для следующей отправки Group уже будет новый
                    storage.PutShort("r7-group-id", (byte)(group + 1));



                    ///TODO: что будет если часть пакетов не будет передана на устройство??
                    await SendPackets(packets, progress);


                    buffer.SaveMessage(new Storage.Message()
                    {
                        Id = messageId,
                        Group = group,
                        TotalParts = (byte)packets.Count,
                        Type = message.Type
                    });


                    return (messageId, packets.Count);
                }
                finally
                {
                    sendLock.Release();
                }
            });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task SendPackets(List<Packet> packets, Action<double> progress = null)
        {
            ///Передаем пакеты на устройство
            for (int i = 0; i < packets.Count; i++)
            {
                var packet = packets[i];
                var __packet = buffer.GetPacket(packet.Id);

                if (__packet?.Transmitted != true)
                {
                    ushort packetId = await SendData(packet.Payload);
                    packet.FrameworkId = packetId;

                    buffer.SavePacket(packet);

                    logger.Log($"[PACKET] Sent to device with Id={packetId}");
                }
                else
                {
                    Debugger.Break();
                }


                double __progress = 100d * ((i + 1) / (double)packets.Count);


                if (packets.Count > 1)
                    progress?.Invoke(__progress);
            }
        }



        public Task<bool> Connect(Guid id, bool force = true, bool throwOnError = false, int attempts = 1) => framework.Connect(id, force, throwOnError, attempts);

        public Task<bool> Connect(IBluetoothDevice device, bool force = true, bool throwOnError = false) => framework.Connect(device, force, throwOnError);

        public Task Disconnect() => framework.Disconnect();

        public Task ForgetDevice() => framework.ForgetDevice();

        public Task SendManual() => framework.SendManual();

        public Task RequestAlert() => framework.RequestAlert();

        public Task Beep() => framework.Beep();

        public Task GetReceivedMessages() => framework.GetReceivedMessages();

        public Task StartDeviceSearch() => framework.StartDeviceSearch();

        public void StopDeviceSearch() => framework.StopDeviceSearch();

        public Task<ushort> SendData(byte[] data) => framework.SendData(data);

        public void Dispose() => framework.Dispose();

        public Task<bool> Reconnect(bool throwOnError = true) => framework.Reconnect(throwOnError: throwOnError);
    }
}
