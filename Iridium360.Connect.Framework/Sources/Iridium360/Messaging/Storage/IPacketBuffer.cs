﻿using Realms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Iridium360.Connect.Framework.Messaging.Storage
{
    /// <summary>
    /// Буфер пакетов
    /// </summary>
    public interface IPacketBuffer
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        Message GetMessageByGroup(uint group, PacketDirection direction);

        /// <summary>
        /// Сохраняем сообщение
        /// </summary>
        /// <param name="message"></param>
        void SaveMessage(Message message);

        /// <summary>
        /// Удаляем сообщение
        /// </summary>
        /// <param name="groupId"></param>
        void DeleteMessage(uint groupId);

        /// <summary>
        /// Получить кол-во сохраненных пакетов сообщения
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        uint GetPacketCount(uint groupId, PacketDirection direction);

        /// <summary>
        /// Получить все сохраненные пакеты сообщения
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        List<Packet> GetPackets(uint groupId, PacketDirection direction);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name=""></param>
        /// <returns></returns>
        Packet GetPacket(int frameworkId);

        /// <summary>
        /// Сохранить пакет
        /// </summary>
        /// <param name="packet"></param>
        void SavePacket(Packet packet);

        /// <summary>
        /// Удалить все пакеты сообщения
        /// </summary>
        /// <param name="groupId"></param>
        void DeletePackets(uint groupId, PacketDirection direction);

        /// <summary>
        /// Отметить, что пакет отправлен
        /// </summary>
        /// <param name="packetId"></param>
        void SetPacketTransmitted(int frameworkId);

        /// <summary>
        /// Отметить, что пакет не отправлен
        /// </summary>
        /// <param name="frameworkId"></param>
        void SetPacketNotTransmitted(int frameworkId);

        /// <summary>
        /// Получить пакет
        /// </summary>
        /// <param name="packetId"></param>
        /// <returns></returns>
        Packet GetPacket(string packetId);
    }






    /// <summary>
    /// 
    /// </summary>
    public static class PacketBufferHelper
    {
        private const string BUFFER_DATABASE_NAME = "buffer.realm";

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal static Realm GetBufferInstance()
        {
            lock (BUFFER_DATABASE_NAME)
            {
                return Realm.GetInstance(GetBufferConfig());
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal static RealmConfiguration GetBufferConfig()
        {
            return new RealmConfiguration(BUFFER_DATABASE_NAME)
            {
                SchemaVersion = 10,
                ObjectClasses = new Type[] { typeof(MessageRealm), typeof(Part) },
                MigrationCallback = (migration, oldSchemaVersion) =>
                {
                }
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static string GetBufferDbPath()
        {
            return GetBufferConfig().DatabasePath;
        }
    }



}
