namespace Rock.Iridium360.Messaging
{
    using System;
    using System.Runtime.CompilerServices;

    public class PayloadMO : MessageMO
    {
        public static PayloadMO Create(byte[] payload)
        {
            PayloadMO dmo1 = new PayloadMO();
            dmo1.Payload = payload;
            return dmo1;
        }

        protected override void pack(BinaryBitWriter writer)
        {
            writer.Write(this.Payload);
        }

        protected override void unpack(byte[] payload)
        {
            this.Payload = payload;
        }

        public override MessageType Type =>
            MessageType.Payload;

        public byte[] Payload { get; protected set; }
    }
}

