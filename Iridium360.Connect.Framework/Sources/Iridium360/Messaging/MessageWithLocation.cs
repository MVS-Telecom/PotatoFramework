﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Iridium360.Connect.Framework.Messaging
{
    public abstract class MessageWithLocation : Message
    {
        public double? Lat { get; protected set; }
        public double? Lon { get; protected set; }
        public int? Alt { get; protected set; }


        protected void WriteLocation(BinaryBitWriter writer)
        {
            if (Direction != Direction.MO)
                throw new InvalidOperationException();

            if (Version >= ProtocolVersion.LocationFix)
            {
                writer.Write((float)Lat, true, 7, 13);
                writer.Write((float)Lon, true, 8, 13);
            }
            else
            {
                writer.Write((float)Lat, true, 7, 9);
                writer.Write((float)Lon, true, 8, 9);
            }


            if (Alt != null)
            {
                writer.Write(true);
                writer.Write((uint)Math.Min(16383, Math.Max(0, Alt.Value)), 14);
            }
            else
            {
                writer.Write(false);
            }
        }

        protected void ReadLocation(BinaryBitReader reader)
        {
            if (Version >= ProtocolVersion.LocationFix)
            {
                Lat = reader.ReadFloat(true, 7, 13);
                Lon = reader.ReadFloat(true, 8, 13);
            }
            else
            {
                Lat = reader.ReadFloat(true, 7, 9);
                Lon = reader.ReadFloat(true, 8, 9);
            }


            bool hasAlt = reader.ReadBoolean();

            if (hasAlt)
                Alt = (int)reader.ReadUInt(14);
        }
    }
}
