using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace InfiniMap
{
    public struct StreamInfo
    {
        public readonly byte[] BsonBuffer;

        public StreamInfo(byte[] bytes)
        {
            BsonBuffer = bytes;
        }
    }

    public class BlockMetadata : DynamicObject
    {
        public Dictionary<string, dynamic> Dictionary;

        public BlockMetadata()
        {
            Dictionary = new Dictionary<string, dynamic>();
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return Dictionary.TryGetValue(binder.Name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (value.GetType().IsPrimitive || value is string || value is DateTime)
            {
                Dictionary[binder.Name] = value;
                return true;
            }

            throw new NotSupportedException("Can only store primitive or string types as a value");
        }

        public StreamInfo Write()
        {
            if (Dictionary.Count >= 1)
            {
                var stream = new MemoryStream();
                var serializer = new JsonSerializer();
                var writer = new BsonWriter(stream);

                serializer.Serialize(writer, Dictionary);
                stream.Seek(0, SeekOrigin.Begin);
                return new StreamInfo(stream.GetBuffer());
            }
            else
            {
                return new StreamInfo(Enumerable.Empty<byte>().ToArray());
            }
        }
    }

    public class Block : ISerialize, IDeserialize
    {
        /// <summary>
        /// Combined BlockId and BlockMeta.
        /// </summary>
        public UInt32 BlockData { get; private set; }

        /// <summary>
        /// Holds the block ID
        /// </summary>
        public UInt16 BlockId
        {
            get { return (UInt16) (BlockData & 0xFFFF); }
            set { BlockData = (((UInt32)value)&0xFFFF) | ((UInt32) BlockMeta << 16); }
        }

        /// <summary>
        /// MetaId attached to the block.
        /// </summary>
        public UInt16 BlockMeta
        {
            get { return (UInt16) ((BlockData >> 16) & 0xFFFF); }
            set { BlockData = (((UInt32)BlockId) & 0xFFFF) | ((UInt32)value << 16); }
        }

        /// <summary>
        /// Quick access for a small set of block Flags
        /// </summary>
        public UInt32 Flags { get; set; }

        /// <summary>
        /// Contains optional extended properties for this specific block instance.
        /// </summary>
        public BlockMetadata ExtendedMetadata = new BlockMetadata();

        public dynamic Metadata
        {
            get
            {
                if (ExtendedMetadata == null)
                {
                    throw new NullReferenceException("ExtendedMetaData was null");
                }
                return ExtendedMetadata;
            }
        }

        public Block() : this(0,0) { }

        public Block(UInt32 blockData, UInt32 flags)
        {
            BlockData = blockData;
            Flags = flags;
        }

        public StreamInfo GetMetadata()
        {
            return ExtendedMetadata.Write();
        }

        public void Write(BinaryWriter stream)
        {
            stream.Write(BlockData);
            stream.Write(Flags);
        }

        public void Read(Stream stream)
        {
            using (var r = new BinaryReader(stream))
            {
                BlockData = r.ReadUInt32();
                Flags = r.ReadUInt32();
            }
        }
    }
}