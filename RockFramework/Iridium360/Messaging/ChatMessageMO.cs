namespace Rock.Iridium360.Messaging
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public class ChatMessageMO : FreeTextMO
    {
        private ChatMessageMO()
        {
        }

        public static ChatMessageMO Create(string chatId, ushort? conversation, string text, string subject = null)
        {
            ChatMessageMO emo1 = new ChatMessageMO();
            emo1.ChatId = chatId;
            emo1.Conversation = conversation;
            emo1.Text = text;
            emo1.Subject = subject;
            return emo1;
        }

        protected override void pack(BinaryBitWriter writer)
        {
            Flags eMPTY = Flags.EMPTY;
            if (!string.IsNullOrEmpty(this.ChatId))
            {
                eMPTY |= Flags.HasChatId;
            }
            if (this.Conversation.HasValue)
            {
                eMPTY |= Flags.HasConversation;
            }
            if (!string.IsNullOrEmpty(this.Subject))
            {
                eMPTY |= Flags.HasSubject;
            }
            if (!string.IsNullOrEmpty(base.Text))
            {
                eMPTY |= Flags.HasText;
            }
            writer.Write((byte)((byte)eMPTY));
            if (eMPTY.HasFlag(Flags.HasChatId))
            {
                Write(writer, this.ChatId);
            }
            if (eMPTY.HasFlag(Flags.HasConversation))
            {
                writer.Write(this.Conversation.Value);
            }
            if (eMPTY.HasFlag(Flags.HasSubject))
            {
                Write(writer, this.Subject);
            }
            if (eMPTY.HasFlag(Flags.HasText))
            {
                Write(writer, base.Text);
            }
            if (eMPTY.HasFlag(Flags.HasLocation))
            {
            }
        }

        protected override void unpack(byte[] payload)
        {
            using (MemoryStream stream = new MemoryStream(payload))
            {
                using (BinaryBitReader reader = new BinaryBitReader((Stream)stream))
                {
                    Flags flags = (Flags)reader.ReadByte();
                    if (flags.HasFlag(Flags.HasChatId))
                    {
                        this.ChatId = Read(reader);
                    }
                    if (flags.HasFlag(Flags.HasConversation))
                    {
                        this.Conversation = new ushort?(reader.ReadUInt16());
                    }
                    if (flags.HasFlag(Flags.HasSubject))
                    {
                        this.Subject = Read(reader);
                    }
                    if (flags.HasFlag(Flags.HasText))
                    {
                        base.Text = Read(reader);
                    }
                    if (flags.HasFlag(Flags.HasLocation))
                    {
                    }
                }
            }
        }

        public override MessageType Type =>
            MessageType.ChatMessageMO;

        public string ChatId { get; private set; }

        public string Subject { get; private set; }

        public ushort? Conversation { get; private set; }

        [Flags]
        public enum Flags
        {
            EMPTY = 0,
            HasChatId = 1,
            HasConversation = 2,
            HasText = 4,
            HasSubject = 8,
            HasLocation = 0x10,
            Reserved_1 = 0x20,
            Reserver_2 = 0x40,
            Reserver_3 = 0x80
        }
    }
}

