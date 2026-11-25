"""Chart from picture (rules without UNO, unit-tested): a picture as RGBA pixels on white for the engine's
``chartFromPicture`` (charts v2 stage 5; same input as the Office web add-in's picture.ts and the COM add-in).

LibreOffice decodes the file (``GraphicProvider.queryGraphic``, any format LibreOffice reads); its ``XBitmap`` gives the
pixels as a Windows DIB (``getDIB``) and, for pictures with transparency, an 8-bit mask (``getMaskDIB``, verified with
LibreOffice 26.8: 255 = fully transparent). This module reads both DIBs, scales large pictures down (the scanner needs no
more than about 1600 px) and puts transparent areas on white. Pure Python, no PIL.
"""

import base64
import struct

MAX_SIDE = 1600
EXTENSIONS = (".png", ".jpg", ".jpeg", ".bmp", ".gif")


def is_picture_name(name):
    return (name or "").lower().endswith(EXTENSIONS)


def scaled_size(w, h, max_side=MAX_SIDE):
    """Target size for a picture of w × h (scaled down to ``max_side``, never up)."""
    k = min(1.0, float(max_side) / max(w, h, 1))
    return max(1, int(round(w * k))), max(1, int(round(h * k)))


def _u16(b, o):
    return struct.unpack_from("<H", b, o)[0]


def _u32(b, o):
    return struct.unpack_from("<I", b, o)[0]


def _i32(b, o):
    return struct.unpack_from("<i", b, o)[0]


def _shift_bits(mask):
    """(shift, maximum) of a bit field mask."""
    if not mask:
        return 0, 0
    shift = 0
    while not (mask >> shift) & 1:
        shift += 1
    return shift, mask >> shift


def decode_dib(data):
    """(width, height, RGB bytes, rows top-down) of a DIB with or without the "BM" file header: 1, 4, 8, 16, 24 and
    32 bits per pixel, uncompressed or bit fields. ValueError for anything else."""
    data = bytes(data)
    if len(data) < 16:
        raise ValueError("not a bitmap")
    base = 14 if data[:2] == b"BM" else 0
    bits_offset = _u32(data, 10) if base else None
    size = _u32(data, base)
    if size == 12:
        w, h, bpp = _u16(data, base + 4), _u16(data, base + 6), _u16(data, base + 10)
        compression, colours, entry = 0, 0, 3
    elif size >= 40:
        w, h, bpp = _i32(data, base + 4), _i32(data, base + 8), _u16(data, base + 14)
        compression, colours, entry = _u32(data, base + 16), _u32(data, base + 32), 4
    else:
        raise ValueError("unknown bitmap header (%d bytes)" % size)
    top_down = h < 0
    h = abs(h)
    if w <= 0 or h == 0:
        raise ValueError("empty bitmap")
    masks = None
    extra = 0
    if compression == 3:
        if size >= 52:
            masks = struct.unpack_from("<III", data, base + 40)
        else:
            masks = struct.unpack_from("<III", data, base + size)
            extra = 12
    elif compression != 0:
        raise ValueError("compressed bitmap (%d) not supported" % compression)
    if bpp <= 8:
        count = colours or (1 << bpp)
        start = base + size + extra
        palette = [bytes((data[start + i * entry + 2], data[start + i * entry + 1], data[start + i * entry]))
                   for i in range(count) if start + i * entry + 2 < len(data)]
        palette += [b"\x00\x00\x00"] * ((1 << bpp) - len(palette))
        if bits_offset is None:
            bits_offset = start + count * entry
    elif bits_offset is None:
        bits_offset = base + size + extra
    stride = ((w * bpp + 31) // 32) * 4
    if bits_offset + stride * h > len(data):
        raise ValueError("bitmap data too short")
    rows = []
    for y in range(h):
        src = y if top_down else h - 1 - y
        rows.append(_row_rgb(data[bits_offset + src * stride: bits_offset + (src + 1) * stride], w, bpp, masks,
                             palette if bpp <= 8 else None))
    return w, h, b"".join(rows)


def _row_rgb(raw, w, bpp, masks, palette):
    if bpp == 24 and masks is None:
        raw = raw[:3 * w]
        out = bytearray(3 * w)
        out[0::3], out[1::3], out[2::3] = raw[2::3], raw[1::3], raw[0::3]
        return bytes(out)
    if bpp == 32 and masks is None:
        raw = raw[:4 * w]
        out = bytearray(3 * w)
        out[0::3], out[1::3], out[2::3] = raw[2::4], raw[1::4], raw[0::4]
        return bytes(out)
    if bpp == 8:
        return b"".join(palette[i] for i in raw[:w])
    if bpp in (1, 2, 4):
        per = 8 // bpp
        top = (1 << bpp) - 1
        idx = [(raw[x // per] >> (8 - bpp * (x % per + 1))) & top for x in range(w)]
        return b"".join(palette[i] for i in idx)
    if bpp in (16, 32):
        if masks is None:   # 16 bits without bit fields: 5-5-5
            masks = (0x7C00, 0x03E0, 0x001F)
        fields = [_shift_bits(m) for m in masks]
        step = bpp // 8
        out = bytearray(3 * w)
        for x in range(w):
            v = int.from_bytes(raw[x * step:(x + 1) * step], "little")
            for c, (shift, top) in enumerate(fields):
                out[3 * x + c] = ((v >> shift) & top) * 255 // top if top else 0
        return bytes(out)
    raise ValueError("%d bits per pixel not supported" % bpp)


def _sample(values, w, h, tw, th, channels):
    """Nearest-neighbour scaling of ``channels``-byte pixels."""
    if (tw, th) == (w, h):
        return values
    xs = [min(w - 1, int((x + 0.5) * w / tw)) for x in range(tw)]
    ys = [min(h - 1, int((y + 0.5) * h / th)) for y in range(th)]
    offsets = [channels * x + c for x in xs for c in range(channels)]
    line = channels * w
    rows = []
    for y in ys:
        row = values[line * y: line * (y + 1)]
        rows.append(bytes(map(row.__getitem__, offsets)))
    return b"".join(rows)


def to_rgba(w, h, rgb, transparency=None, max_side=MAX_SIDE):
    """(w, h, RGBA bytes) for the scanner: scaled down to ``max_side``, pixels with ``transparency`` (one byte per
    pixel, 255 = transparent) blended onto white, alpha 255 everywhere."""
    tw, th = scaled_size(w, h, max_side)
    rgb = _sample(rgb, w, h, tw, th, 3)
    if transparency is not None and len(transparency) == w * h and any(transparency):
        t_all = _sample(bytes(transparency), w, h, tw, th, 1)
        out = bytearray(rgb)
        for i, t in enumerate(t_all):
            if t:
                k = 3 * i
                keep = 255 - t
                out[k] = (out[k] * keep + 255 * t + 127) // 255
                out[k + 1] = (out[k + 1] * keep + 255 * t + 127) // 255
                out[k + 2] = (out[k + 2] * keep + 255 * t + 127) // 255
        rgb = bytes(out)
    n = tw * th
    rgba = bytearray(4 * n)
    rgba[0::4], rgba[1::4], rgba[2::4] = rgb[0::3], rgb[1::3], rgb[2::3]
    rgba[3::4] = b"\xff" * n
    return tw, th, bytes(rgba)


def mask_values(mask_dib, w, h):
    """The transparency per pixel (one byte, 255 = transparent) from ``getMaskDIB``, or None if it does not match the
    picture (empty, other size)."""
    if not mask_dib:
        return None
    try:
        mw, mh, rgb = decode_dib(mask_dib)
    except ValueError:
        return None
    if (mw, mh) != (w, h):
        return None
    return rgb[0::3]


def request(dib, mask_dib=None, max_side=MAX_SIDE):
    """Parameters of the engine method ``chartFromPicture`` (without session and position): {w, h, payload}."""
    w, h, rgb = decode_dib(dib)
    tw, th, rgba = to_rgba(w, h, rgb, mask_values(mask_dib, w, h), max_side)
    return {"w": tw, "h": th, "payload": base64.b64encode(rgba).decode("ascii")}
