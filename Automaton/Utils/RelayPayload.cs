﻿using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.GeneratedSheets;
using System.IO;

namespace Automaton.Utils;
public sealed class RelayPayload(MapLinkPayload mapLink, uint? worldId, uint? instance) : DalamudLinkPayload
{
    private const byte EmbeddedInfoTypeByte = (byte)(EmbeddedInfoType.DalamudLink + 2);

    public MapLinkPayload MapLink => mapLink;
    public World? World => worldId.HasValue ? GetRow<World>(worldId.Value) : default;
    public uint? Instance => instance ?? default;

    public override PayloadType Type => PayloadType.Unknown;

    private RelayPayload() : this(new MapLinkPayload(0, 0, 0, 0), 0, 0) { }

    protected override byte[] EncodeImpl()
    {
        var data = new List<byte>();
        data.AddRange(mapLink.Encode());
        data.AddRange(MakeInteger(worldId ?? 0));
        data.AddRange(MakeInteger(instance ?? 0));

        var length = 2 + (byte)data.Count;
        return [
            START_BYTE,
            (byte)SeStringChunkType.Interactable,
            (byte)length,
            EmbeddedInfoTypeByte,
            .. data,
            END_BYTE,
        ];
    }

    protected override void DecodeImpl(BinaryReader reader, long _)
    {
        mapLink = (MapLinkPayload)Decode(reader);
        worldId = GetInteger(reader);
        instance = GetInteger(reader);
    }

    public override string ToString() => $"{nameof(RelayPayload)}[{mapLink}, {worldId}, {instance}]";

    public RawPayload ToRawPayload() => new(EncodeImpl());

    public static RelayPayload? Parse(RawPayload payload)
    {
        using var stream = new MemoryStream(payload.Data);
        using var reader = new BinaryReader(stream);

        if (reader.ReadByte() != START_BYTE)
        {
            return default;
        }

        if (reader.ReadByte() != (byte)SeStringChunkType.Interactable)
        {
            return default;
        }

        var length = reader.ReadByte();
        if (reader.ReadByte() != EmbeddedInfoTypeByte)
        {
            return default;
        }

        var result = new RelayPayload();
        result.DecodeImpl(reader, /* unused */ default);
        return result;
    }
}
