namespace Rock.Iridium360.Messaging
{
    using System;

    public abstract class MessageMT : Message
    {
        protected MessageMT()
        {
        }

        public override Rock.Iridium360.Messaging.Direction Direction =>
            Rock.Iridium360.Messaging.Direction.MT;
    }
}

