using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace FamilyPlanner
{
    static class UiCursor
    {
        static MemoryStream dragMoveStream;
        static MemoryStream dragCopyStream;
        static readonly Cursor dragMove = CreateDragMove();
        static readonly Cursor dragCopy = CreateDragCopy();

        static Cursor CreateDragMove()
        {
            try
            {
                dragMoveStream = new MemoryStream(BuildDragMoveData());
                return new Cursor(dragMoveStream);
            }
            catch { return Cursors.Arrow; }
        }

        static Cursor CreateDragCopy()
        {
            try
            {
                dragCopyStream = new MemoryStream(BuildDragMoveData(true));
                return new Cursor(dragCopyStream);
            }
            catch { return Cursors.Arrow; }
        }

        // 앱 스킨의 배경색과 무관하게 Windows 표준 포인터를 사용한다.
        // 같은 크기 조절 기능은 파스텔·블랙에서 항상 같은 모양과 핫스폿을 갖는다.
        public static Cursor ResizeNwSe { get { return Cursors.SizeNWSE; } }
        public static Cursor ResizeNeSw { get { return Cursors.SizeNESW; } }
        public static Cursor ResizeHorizontal { get { return Cursors.SizeWE; } }
        public static Cursor ResizeVertical { get { return Cursors.SizeNS; } }
        public static Cursor DragMove { get { return dragMove; } }
        public static Cursor DragCopy { get { return dragCopy; } }
        public static bool ControlDown { get { return (GetAsyncKeyState(0x11) & 0x8000) != 0; } }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int virtualKey);

        static byte[] BuildDragMoveData(bool copy = false)
        {
            const int size = 32, maskStride = 4;
            var pixels = new byte[size * size * 4];
            var mask = new byte[maskStride * size];
            for (var i = 0; i < mask.Length; i++) mask[i] = 255;
            Action<int, int, byte, byte, byte> setPixel = (x, y, r, g, b) =>
            {
                if (x < 0 || x >= size || y < 0 || y >= size) return;
                var row = size - 1 - y; var offset = (row * size + x) * 4;
                pixels[offset] = b; pixels[offset + 1] = g; pixels[offset + 2] = r; pixels[offset + 3] = 255;
                mask[row * maskStride + x / 8] &= (byte)~(0x80 >> (x % 8));
            };
            var arrow = new[] { "100000000000", "110000000000", "111000000000", "111100000000",
                "111110000000", "111111000000", "111111100000", "111111110000", "111111111000",
                "111111111100", "111111000000", "110011000000", "100011000000", "000001100000",
                "000001100000", "000000110000", "000000110000" };
            for (var y = 0; y < arrow.Length; y++) for (var x = 0; x < arrow[y].Length; x++)
                if (arrow[y][x] == '1') for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
                    setPixel(x + dx + 1, y + dy + 1, 255, 255, 255);
            for (var y = 0; y < arrow.Length; y++) for (var x = 0; x < arrow[y].Length; x++)
                if (arrow[y][x] == '1') setPixel(x + 1, y + 1, 20, 20, 20);
            // 작은 ONHARU 카드: 오른쪽 아래 그림자, 흰 카드, 바이올렛 헤더와 내용선.
            for (var y = 18; y <= 28; y++) for (var x = 16; x <= 30; x++) setPixel(x, y, 71, 85, 105);
            for (var y = 16; y <= 26; y++) for (var x = 14; x <= 29; x++)
                if (x == 14 || x == 29 || y == 16 || y == 26) setPixel(x, y, 30, 41, 59);
                else setPixel(x, y, 255, 255, 255);
            for (var x = 16; x <= 27; x++) { setPixel(x, 18, 99, 91, 255); setPixel(x, 19, 99, 91, 255); }
            for (var x = 17; x <= 26; x++) setPixel(x, 22, 148, 163, 184);
            for (var x = 17; x <= 23; x++) setPixel(x, 24, 203, 213, 225);
            if (copy)
            {
                // Ctrl 복사 표식: 흰 배지 안의 선명한 초록색 +.
                for (var x = 22; x <= 31; x++) for (var y = 8; y <= 17; y++)
                    setPixel(x, y, x == 22 || x == 31 || y == 8 || y == 17 ? (byte)22 : (byte)255,
                        x == 22 || x == 31 || y == 8 || y == 17 ? (byte)163 : (byte)255,
                        x == 22 || x == 31 || y == 8 || y == 17 ? (byte)74 : (byte)255);
                for (var x = 24; x <= 29; x++) for (var y = 10; y <= 15; y++)
                    if ((x >= 26 && x <= 27) || (y >= 12 && y <= 13)) setPixel(x, y, 22, 163, 74);
            }
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0); writer.Write((ushort)2); writer.Write((ushort)1);
                writer.Write((byte)size); writer.Write((byte)size); writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)1); writer.Write((uint)(40 + pixels.Length + mask.Length)); writer.Write((uint)22);
                writer.Write((uint)40); writer.Write(size); writer.Write(size * 2); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write((uint)0); writer.Write((uint)pixels.Length); writer.Write(0); writer.Write(0); writer.Write((uint)0); writer.Write((uint)0);
                writer.Write(pixels); writer.Write(mask); writer.Flush(); return stream.ToArray();
            }
        }

    }
}
