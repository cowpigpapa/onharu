#include <windows.h>
#include <commctrl.h>
#include <windowsx.h>
#include <cstring>
#include <cstdio>
#include "LayerShared.h"
#include "SharedFrame.h"

static HINSTANCE g_instance;
static HWND g_defView;
static HWND g_iconList;
static UINT g_initMessage;
static UINT g_stopMessage;
static UINT g_desktopActionMessage;
static bool g_resizing;
static bool g_resizeFromTopLeft;
static bool g_moving;
static int g_pointerDragKind;
static bool g_onharuOwnsUndo;
static HHOOK g_keyboardHook;
static POINT g_resizeStart;
static int g_resizeWidth;
static int g_resizeHeight;
static int g_resizeLeft;
static int g_resizeTop;
static HANDLE g_frameMapping;
static BYTE* g_frameView;
static SIZE_T g_frameViewBytes;
static HDC g_cacheDC;
static HBITMAP g_cacheBitmap;
static HGDIOBJ g_cacheOldBitmap;
static BYTE* g_cacheBits;
static LONG g_cacheWidth;
static LONG g_cacheHeight;
static LONG g_cacheGeneration;
static HDC g_baseDC;
static HBITMAP g_baseBitmap;
static HGDIOBJ g_baseOldBitmap;
static HDC g_finalDC;
static HBITMAP g_finalBitmap;
static HGDIOBJ g_finalOldBitmap;
static LONG g_surfaceWidth;
static LONG g_surfaceHeight;
static LONG g_surfaceLeft;
static LONG g_surfaceTop;
static LONG g_finalGeneration;
static BYTE g_finalOpacity;
static bool g_baseValid;
static DWORD g_originalListViewStyle;
static bool g_changedDoubleBuffer;
static bool g_insideListPaint;
static bool g_hasListPaintUpdate;
static RECT g_listPaintUpdate;
static constexpr UINT_PTR kListSubclassId = 0x4F4E4841;
static constexpr UINT_PTR kDefViewSubclassId = 0x4F4E4842;
static constexpr DWORD kHitMapMagic = 0x32544948; // HIT2
static constexpr UINT kDesktopActionMessage = WM_APP + 0x4F;
static bool OpenFrame();
static void PostDesktopAction(WPARAM action, LPARAM value);

static LRESULT CALLBACK KeyboardHook(int code, WPARAM wParam, LPARAM lParam)
{
    if (code == HC_ACTION && wParam == 'Z' && g_onharuOwnsUndo &&
        (GetKeyState(VK_CONTROL) & 0x8000) != 0) {
        const bool keyUp = (lParam & (1LL << 31)) != 0;
        const bool repeated = (lParam & (1LL << 30)) != 0;
        if (!keyUp && !repeated) PostDesktopAction(110, 0);
        return 1;
    }
    return CallNextHookEx(g_keyboardHook, code, wParam, lParam);
}

struct OpacityProfileStats
{
    LONG count;
    double baseTotalUs, blendTotalUs, blitTotalUs;
    double baseMaxUs, blendMaxUs, blitMaxUs;
};

static OpacityProfileStats g_opacityProfile = {};

static double QpcMicroseconds(const LARGE_INTEGER& start, const LARGE_INTEGER& end)
{
    static LARGE_INTEGER frequency = [] { LARGE_INTEGER value = {}; QueryPerformanceFrequency(&value); return value; }();
    return frequency.QuadPart > 0 ? (end.QuadPart - start.QuadPart) * 1000000.0 / frequency.QuadPart : 0;
}

static void RecordOpacityProfile(double baseUs, double blendUs, double blitUs, LONG width, LONG height)
{
    auto& stats = g_opacityProfile;
    ++stats.count; stats.baseTotalUs += baseUs; stats.blendTotalUs += blendUs; stats.blitTotalUs += blitUs;
    stats.baseMaxUs = max(stats.baseMaxUs, baseUs); stats.blendMaxUs = max(stats.blendMaxUs, blendUs); stats.blitMaxUs = max(stats.blitMaxUs, blitUs);
    if (stats.count < 60) return;
    wchar_t folder[MAX_PATH] = {}; wchar_t path[MAX_PATH] = {};
    if (!GetTempPathW(MAX_PATH, folder)) { stats = {}; return; }
    swprintf_s(path, L"%sONHARU-opacity-qpc.log", folder);
    HANDLE file = CreateFileW(path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file != INVALID_HANDLE_VALUE) {
        char line[320] = {};
        const int length = sprintf_s(line,
            "size=%ldx%ld samples=%ld base_avg_us=%.1f base_max_us=%.1f blend_avg_us=%.1f blend_max_us=%.1f blit_avg_us=%.1f blit_max_us=%.1f total_avg_us=%.1f\r\n",
            width, height, stats.count, stats.baseTotalUs / stats.count, stats.baseMaxUs,
            stats.blendTotalUs / stats.count, stats.blendMaxUs, stats.blitTotalUs / stats.count, stats.blitMaxUs,
            (stats.baseTotalUs + stats.blendTotalUs + stats.blitTotalUs) / stats.count);
        DWORD written = 0; if (length > 0) WriteFile(file, line, static_cast<DWORD>(length), &written, nullptr); CloseHandle(file);
    }
    stats = {};
}

static void PostDesktopAction(WPARAM action, LPARAM value)
{
    HWND sink = nullptr;
    if ((g_frameView || OpenFrame()) && g_frameViewBytes >= sizeof(OnharuFrameHeader)) {
        const auto* header = reinterpret_cast<const OnharuFrameHeader*>(g_frameView);
        sink = reinterpret_cast<HWND>(static_cast<UINT_PTR>(header->reserved[3]));
    }
    if (sink && g_desktopActionMessage) PostMessageW(sink, g_desktopActionMessage, action, value);
}

#pragma pack(push, 4)
struct HitMapHeader { DWORD magic; LONG generation; LONG count; LONG reserved; };
struct HitMapRecord { LONG left, top, right, bottom, clickAction, doubleAction, value; };
#pragma pack(pop)

static int HitKindAt(const OnharuFrameHeader* header, int x, int y)
{
    if (!header || header->reserved[1] == 0) return 0;
    const SIZE_T offset = sizeof(OnharuFrameHeader) + static_cast<SIZE_T>(header->reserved[1]) * 2;
    if (offset + sizeof(HitMapHeader) > g_frameViewBytes) return 0;
    const auto* hitHeader = reinterpret_cast<const HitMapHeader*>(g_frameView + offset);
    if (hitHeader->magic != kHitMapMagic || hitHeader->generation != header->generation ||
        hitHeader->count < 0 || hitHeader->count > 256 ||
        offset + sizeof(HitMapHeader) + static_cast<SIZE_T>(hitHeader->count) * sizeof(HitMapRecord) > g_frameViewBytes) return 0;
    const auto* records = reinterpret_cast<const HitMapRecord*>(hitHeader + 1);
    for (LONG index = hitHeader->count - 1; index >= 0; --index) {
        const auto& record = records[index];
        if (x >= record.left && x < record.right && y >= record.top && y < record.bottom)
            return record.value;
    }
    return 0;
}

static const HitMapRecord* HitRecordAt(const OnharuFrameHeader* header, int x, int y)
{
    if (!header || header->reserved[1] == 0) return nullptr;
    const SIZE_T offset = sizeof(OnharuFrameHeader) + static_cast<SIZE_T>(header->reserved[1]) * 2;
    if (offset + sizeof(HitMapHeader) > g_frameViewBytes) return nullptr;
    const auto* hitHeader = reinterpret_cast<const HitMapHeader*>(g_frameView + offset);
    if (hitHeader->magic != kHitMapMagic || hitHeader->generation != header->generation ||
        hitHeader->count < 0 || hitHeader->count > 256 ||
        offset + sizeof(HitMapHeader) + static_cast<SIZE_T>(hitHeader->count) * sizeof(HitMapRecord) > g_frameViewBytes) return nullptr;
    const auto* records = reinterpret_cast<const HitMapRecord*>(hitHeader + 1);
    for (LONG index = hitHeader->count - 1; index >= 0; --index) {
        const auto& record = records[index];
        if (x >= record.left && x < record.right && y >= record.top && y < record.bottom) return &record;
    }
    return nullptr;
}

static bool HasCurrentHitMap(const OnharuFrameHeader* header)
{
    if (!header || header->reserved[1] == 0) return false;
    const SIZE_T offset = sizeof(OnharuFrameHeader) + static_cast<SIZE_T>(header->reserved[1]) * 2;
    if (offset + sizeof(HitMapHeader) > g_frameViewBytes) return false;
    const auto* hitHeader = reinterpret_cast<const HitMapHeader*>(g_frameView + offset);
    return hitHeader->magic == kHitMapMagic && hitHeader->generation == header->generation &&
        hitHeader->count >= 0 && hitHeader->count <= 256 &&
        offset + sizeof(HitMapHeader) + static_cast<SIZE_T>(hitHeader->count) * sizeof(HitMapRecord) <= g_frameViewBytes;
}

static void CloseFrame()
{
    if (g_cacheDC && g_cacheOldBitmap) SelectObject(g_cacheDC, g_cacheOldBitmap);
    if (g_cacheBitmap) DeleteObject(g_cacheBitmap);
    if (g_cacheDC) DeleteDC(g_cacheDC);
    g_cacheDC = nullptr;
    g_cacheBitmap = nullptr;
    g_cacheOldBitmap = nullptr;
    g_cacheBits = nullptr;
    g_cacheWidth = 0;
    g_cacheHeight = 0;
    g_cacheGeneration = 0;
    if (g_baseDC && g_baseOldBitmap) SelectObject(g_baseDC, g_baseOldBitmap);
    if (g_finalDC && g_finalOldBitmap) SelectObject(g_finalDC, g_finalOldBitmap);
    if (g_baseBitmap) DeleteObject(g_baseBitmap);
    if (g_finalBitmap) DeleteObject(g_finalBitmap);
    if (g_baseDC) DeleteDC(g_baseDC);
    if (g_finalDC) DeleteDC(g_finalDC);
    g_baseDC = nullptr; g_baseBitmap = nullptr; g_baseOldBitmap = nullptr;
    g_finalDC = nullptr; g_finalBitmap = nullptr; g_finalOldBitmap = nullptr;
    g_surfaceWidth = 0; g_surfaceHeight = 0; g_finalGeneration = 0; g_baseValid = false;
    if (g_frameView) UnmapViewOfFile(g_frameView);
    if (g_frameMapping) CloseHandle(g_frameMapping);
    g_frameView = nullptr;
    g_frameMapping = nullptr;
    g_frameViewBytes = 0;
}

static bool EnsureCompositeSurfaces(HDC dc, LONG width, LONG height)
{
    if (g_baseDC && g_finalDC && g_surfaceWidth == width && g_surfaceHeight == height) return true;
    if (g_baseDC && g_baseOldBitmap) SelectObject(g_baseDC, g_baseOldBitmap);
    if (g_finalDC && g_finalOldBitmap) SelectObject(g_finalDC, g_finalOldBitmap);
    if (g_baseBitmap) DeleteObject(g_baseBitmap);
    if (g_finalBitmap) DeleteObject(g_finalBitmap);
    if (g_baseDC) DeleteDC(g_baseDC);
    if (g_finalDC) DeleteDC(g_finalDC);
    g_baseDC = CreateCompatibleDC(dc); g_finalDC = CreateCompatibleDC(dc);
    g_baseBitmap = CreateCompatibleBitmap(dc, width, height);
    g_finalBitmap = CreateCompatibleBitmap(dc, width, height);
    if (!g_baseDC || !g_finalDC || !g_baseBitmap || !g_finalBitmap) return false;
    g_baseOldBitmap = SelectObject(g_baseDC, g_baseBitmap);
    g_finalOldBitmap = SelectObject(g_finalDC, g_finalBitmap);
    g_surfaceWidth = width; g_surfaceHeight = height;
    g_finalGeneration = 0; g_baseValid = false;
    return true;
}

static bool EnsureCache(HDC dc, LONG width, LONG height)
{
    if (g_cacheDC && g_cacheWidth == width && g_cacheHeight == height) return true;
    if (g_cacheDC && g_cacheOldBitmap) SelectObject(g_cacheDC, g_cacheOldBitmap);
    if (g_cacheBitmap) DeleteObject(g_cacheBitmap);
    if (g_cacheDC) DeleteDC(g_cacheDC);
    g_cacheDC = CreateCompatibleDC(dc);
    BITMAPINFO bitmap = {};
    bitmap.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bitmap.bmiHeader.biWidth = width;
    bitmap.bmiHeader.biHeight = -height;
    bitmap.bmiHeader.biPlanes = 1;
    bitmap.bmiHeader.biBitCount = 32;
    bitmap.bmiHeader.biCompression = BI_RGB;
    g_cacheBitmap = CreateDIBSection(dc, &bitmap, DIB_RGB_COLORS,
        reinterpret_cast<void**>(&g_cacheBits), nullptr, 0);
    if (!g_cacheDC || !g_cacheBitmap || !g_cacheBits) {
        if (g_cacheBitmap) DeleteObject(g_cacheBitmap);
        if (g_cacheDC) DeleteDC(g_cacheDC);
        g_cacheDC = nullptr;
        g_cacheBitmap = nullptr;
        g_cacheBits = nullptr;
        return false;
    }
    g_cacheOldBitmap = SelectObject(g_cacheDC, g_cacheBitmap);
    g_cacheWidth = width;
    g_cacheHeight = height;
    g_cacheGeneration = 0;
    return true;
}

static bool OpenFrame()
{
    if (g_frameView) return true;
    g_frameMapping = OpenFileMappingW(FILE_MAP_READ, FALSE, ONHARU_FRAME_MAPPING);
    if (!g_frameMapping) return false;
    g_frameView = static_cast<BYTE*>(MapViewOfFile(g_frameMapping, FILE_MAP_READ, 0, 0, 0));
    if (!g_frameView) { CloseFrame(); return false; }
    MEMORY_BASIC_INFORMATION memory = {};
    if (!VirtualQuery(g_frameView, &memory, sizeof(memory))) { CloseFrame(); return false; }
    g_frameViewBytes = memory.RegionSize;
    return true;
}

static bool DrawSharedFrame(HDC dc)
{
    if (!dc || (!g_frameView && !OpenFrame()) || g_frameViewBytes < sizeof(OnharuFrameHeader)) return false;
    const auto* header = reinterpret_cast<const OnharuFrameHeader*>(g_frameView);
    const LONG generation = header->generation;
    const LONG slot = header->activeSlot;
    MemoryBarrier();
    if (header->magic != ONHARU_FRAME_MAGIC || header->version != ONHARU_FRAME_VERSION ||
        header->width <= 0 || header->height <= 0 || header->width > 8192 || header->height > 8192 ||
        header->stride != header->width * 4 || header->slotBytes != header->stride * header->height ||
        header->panelWidth != header->width || header->panelHeight != header->height ||
        slot < 0 || slot > 1 || generation <= 0) return false;
    const SIZE_T slotCapacity = header->reserved[1] >= static_cast<DWORD>(header->slotBytes) ? header->reserved[1] : header->slotBytes;
    const SIZE_T pixelOffset = sizeof(OnharuFrameHeader) + static_cast<SIZE_T>(slot) * slotCapacity;
    if (pixelOffset + static_cast<SIZE_T>(header->slotBytes) > g_frameViewBytes) return false;

    if (!EnsureCache(dc, header->width, header->height)) return false;
    if (g_cacheGeneration != generation) {
        std::memcpy(g_cacheBits, g_frameView + pixelOffset, header->slotBytes);
        MemoryBarrier();
        if (generation != header->generation) return false;
        g_cacheGeneration = generation;
        // A newly published frame must be composited over the current clean
        // Explorer background. Reusing the prior base at the same coordinates
        // recursively retained the old translucent frame until the panel moved.
        g_baseValid = false;
    }
    RECT panel = { header->panelLeft, header->panelTop,
        header->panelLeft + header->width, header->panelTop + header->height };
    RECT clip = {};
    if (GetClipBox(dc, &clip) == ERROR || !IntersectRect(&clip, &clip, &panel)) return true;
    if (g_insideListPaint && g_hasListPaintUpdate && !IntersectRect(&clip, &clip, &g_listPaintUpdate)) return true;
    const BYTE opacity = static_cast<BYTE>(header->reserved[0] > 255 ? 255 : header->reserved[0]);
    // Build the final surface once from Explorer's freshly painted PREPAINT DC.
    // Subsequent icon drag/selection paints use one cached BitBlt, avoiding both
    // the fallback-colour base cache and repeated AlphaBlend flicker.
    if (!EnsureCompositeSurfaces(dc, header->width, header->height)) return false;
    if (g_finalGeneration != generation || g_finalOpacity != opacity) {
        if (!AlphaBlend(dc, panel.left, panel.top, header->width, header->height,
            g_cacheDC, 0, 0, header->width, header->height,
            BLENDFUNCTION { AC_SRC_OVER, 0, opacity, AC_SRC_ALPHA })) return false;
        if (!BitBlt(g_finalDC, 0, 0, header->width, header->height,
            dc, panel.left, panel.top, SRCCOPY)) return false;
        g_finalGeneration = generation; g_finalOpacity = opacity;
    }
    return BitBlt(dc, clip.left, clip.top, clip.right - clip.left, clip.bottom - clip.top,
        g_finalDC, clip.left - panel.left, clip.top - panel.top, SRCCOPY) != FALSE;
}

static bool IsHandCursorPoint(const OnharuFrameHeader* header, int x, int y)
{
    if (!header || x < 0 || y < 0 || x >= header->width || y >= header->height) return false;
    const int calendarWidth = header->reserved[2] > 0 ? static_cast<int>(header->reserved[2]) : header->width;

    const int hitKind = HitKindAt(header, x, y);
    if (hitKind != 0) return hitKind == 1 || hitKind == 5 || hitKind == 6;
    // A current hit map is the authoritative cursor source.  The old broad
    // coordinate estimates are only a recovery fallback for legacy/missing maps.
    if (HasCurrentHitMap(header)) return false;

    if (x >= header->width - 46 && x < header->width - 10 && y >= 6 && y < 42) return true;
    if (calendarWidth == header->width - 34 && x >= header->width - 34 && y >= 86 && y < 120) return true;
    if (y >= 20 && y <= 60) {
        if (header->width >= 1000) {
            const int left = header->width - 632;
            if ((x >= left && x < left + 273) ||
                (x >= left + 407 && x < left + 499) ||
                (x >= left + 507 && x < left + 545) ||
                (x >= left + 553 && x < left + 591)) return true;
        } else if ((x >= calendarWidth - 215 && x < calendarWidth - 46) ||
                   (x >= 270 && x < 342) ||
                   (x >= 468 && x < 500) ||
                   (x >= 505 && x < 537) ||
                   (x >= 542 && x < 574)) return true;
    }

    if (y >= 117 && y < header->height - 42) {
        // reserved[3] is the action-sink HWND in V3, not the old V2 flags.
        // The exact WPF visual-tree hit test runs after the click; this estimate
        // is cursor-only and must never interpret HWND bits as layout flags.
        const int cellWidth = (calendarWidth - 58) / 7;
        const int relativeX = x - 29;
        const int column = cellWidth > 0 ? relativeX / cellWidth : -1;
        const int cellHeight = max(63, (header->height - 164) / 6);
        const int row = (y - 117) / cellHeight;
        return relativeX >= 0 && column >= 0 && column < 7 && row >= 0 && row < 6;
    }
    return false;
}

static LRESULT CALLBACK ListViewSubclass(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam,
    UINT_PTR, DWORD_PTR)
{
    if (message == WM_KILLFOCUS) g_onharuOwnsUndo = false;
    if (message == WM_KEYDOWN && wParam == 'Z' && g_onharuOwnsUndo &&
        (GetKeyState(VK_CONTROL) & 0x8000) != 0) {
        if ((lParam & (1LL << 30)) == 0) PostDesktopAction(110, 0);
        return 0;
    }
    if (message == WM_PAINT) {
        g_hasListPaintUpdate = GetUpdateRect(hwnd, &g_listPaintUpdate, FALSE) != FALSE;
        g_insideListPaint = true;
        const LRESULT result = DefSubclassProc(hwnd, message, wParam, lParam);
        g_insideListPaint = false;
        g_hasListPaintUpdate = false;
        return result;
    }
    if (message == WM_SETCURSOR && LOWORD(lParam) == HTCLIENT && g_frameView) {
        POINT point = {}; GetCursorPos(&point); ScreenToClient(hwnd, &point);
        LVHITTESTINFO hit = {}; hit.pt = point;
        if (SendMessageW(hwnd, LVM_HITTEST, 0, reinterpret_cast<LPARAM>(&hit)) < 0) {
            const auto* header = reinterpret_cast<const OnharuFrameHeader*>(g_frameView);
            if (IsHandCursorPoint(header, point.x - header->panelLeft, point.y - header->panelTop)) {
                SetCursor(LoadCursorW(nullptr, IDC_HAND)); return TRUE;
            }
        }
    }
    if (message == WM_MOUSEWHEEL && g_frameView && g_desktopActionMessage) {
        const auto* header = reinterpret_cast<const OnharuFrameHeader*>(g_frameView);
        POINT point = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        ScreenToClient(hwnd, &point);
        LVHITTESTINFO hit = {}; hit.pt = point;
        const int icon = static_cast<int>(SendMessageW(hwnd, LVM_HITTEST, 0, reinterpret_cast<LPARAM>(&hit)));
        const int x = point.x - header->panelLeft;
        const int y = point.y - header->panelTop;
        const bool insideOnharu = icon < 0 && x >= 0 && y >= 0 && x < header->width && y < header->height;
        if (insideOnharu) {
            PostDesktopAction(GET_WHEEL_DELTA_WPARAM(wParam) > 0 ? 102 : 103,
                static_cast<LPARAM>((x & 0xFFFF) | ((y & 0xFFFF) << 16)));
            return 0;
        }
    }
    if (message == WM_RBUTTONDOWN && g_frameView && g_desktopActionMessage) {
        const auto* header = reinterpret_cast<const OnharuFrameHeader*>(g_frameView);
        POINT point = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        const int x = point.x - header->panelLeft; const int y = point.y - header->panelTop;
        if (HitKindAt(header, x, y) == 4) { PostDesktopAction(28, 0); return 0; }
    }
    if ((message == WM_RBUTTONUP || message == WM_CONTEXTMENU) && g_frameView && g_desktopActionMessage) {
        const auto* header = reinterpret_cast<const OnharuFrameHeader*>(g_frameView);
        POINT point = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        if (message == WM_CONTEXTMENU) ScreenToClient(hwnd, &point);
        const int x = point.x - header->panelLeft; const int y = point.y - header->panelTop;
        if (HitKindAt(header, x, y) == 4) return 0;
    }
    if (message == WM_MOUSEMOVE && g_resizing && g_desktopActionMessage) {
        POINT point = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        const int width = g_resizeFromTopLeft ? max(820, g_resizeWidth - point.x + g_resizeStart.x) : g_resizeWidth + point.x - g_resizeStart.x;
        const int height = g_resizeFromTopLeft ? max(560, g_resizeHeight - point.y + g_resizeStart.y) : g_resizeHeight + point.y - g_resizeStart.y;
        const LPARAM packed = static_cast<LPARAM>((width & 0xFFFF) | ((height & 0xFFFF) << 16));
        PostDesktopAction(10, packed);
        if (g_resizeFromTopLeft) {
            const int left = g_resizeLeft + g_resizeWidth - width; const int top = g_resizeTop + g_resizeHeight - height;
            PostDesktopAction(30, static_cast<LPARAM>((left & 0xFFFF) | ((top & 0xFFFF) << 16)));
        }
        return 0;
    }
    if (message == WM_MOUSEMOVE && g_moving && g_desktopActionMessage) {
        POINT point = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        const int left = g_resizeLeft + point.x - g_resizeStart.x; const int top = g_resizeTop + point.y - g_resizeStart.y;
        PostDesktopAction(30, static_cast<LPARAM>((left & 0xFFFF) | ((top & 0xFFFF) << 16))); return 0;
    }
    if (message == WM_MOUSEMOVE && g_pointerDragKind != 0 && g_frameView && g_desktopActionMessage) {
        const auto* header = reinterpret_cast<const OnharuFrameHeader*>(g_frameView);
        POINT point = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        const int x = point.x - header->panelLeft; const int y = point.y - header->panelTop;
        PostDesktopAction(g_pointerDragKind == 2 ? 104 : 107,
            static_cast<LPARAM>((x & 0xFFFF) | ((y & 0xFFFF) << 16)));
        return 0;
    }
    if (message == WM_LBUTTONUP && g_resizing) {
        g_resizing = false; ReleaseCapture(); PostDesktopAction(11, 0);
        if (g_resizeFromTopLeft) PostDesktopAction(31, 0); return 0;
    }
    if (message == WM_LBUTTONUP && g_moving) {
        g_moving = false; ReleaseCapture(); PostDesktopAction(31, 0); return 0;
    }
    if (message == WM_LBUTTONUP && g_pointerDragKind != 0) {
        g_pointerDragKind = 0; ReleaseCapture(); PostDesktopAction(105, 0); return 0;
    }
    if ((message == WM_LBUTTONDOWN || message == WM_LBUTTONDBLCLK) && g_frameView && g_desktopActionMessage) {
        const auto* header = reinterpret_cast<const OnharuFrameHeader*>(g_frameView);
        POINT point = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        if (message == WM_LBUTTONDOWN) PostDesktopAction(29, 0);
        LVHITTESTINFO hit = {}; hit.pt = point;
        const int icon = static_cast<int>(SendMessageW(hwnd, LVM_HITTEST, 0, reinterpret_cast<LPARAM>(&hit)));
        const int x = point.x - header->panelLeft;
        const int y = point.y - header->panelTop;
        // Desktop icons are always the top interaction layer. ONHARU receives
        // the click only when Explorer did not hit an icon at this point.
        const bool insideOnharu = icon < 0 && x >= 0 && y >= 0 && x < header->width && y < header->height;
        if (message == WM_LBUTTONDOWN) g_onharuOwnsUndo = insideOnharu;
        if (insideOnharu) {
            const int hitKind = HitKindAt(header, x, y);
            // Keep keyboard focus for fixed-mode Ctrl+Z, but do not foreground the
            // Explorer root. Foregrounding Explorer repaints the whole desktop and
            // visibly flashes while the fixed surface is exchanged for the WPF one.
            if (message == WM_LBUTTONDOWN && hitKind != 7) {
                SetFocus(hwnd);
            }
            // Match Explorer's normal blank-desktop click: ONHARU consumes this
            // message, so clear any selected icon explicitly before dispatching it.
            if (message == WM_LBUTTONDOWN && ListView_GetSelectedCount(hwnd) > 0) {
                ListView_SetItemState(hwnd, -1, 0, LVIS_SELECTED | LVIS_FOCUSED);
                SendMessageW(hwnd, LVM_SETSELECTIONMARK, 0, -1);
            }
            if (message == WM_LBUTTONDOWN && hitKind == 5) {
                PostDesktopAction(108, 0); return 0;
            }
            if (message == WM_LBUTTONDOWN && hitKind == 6) {
                PostDesktopAction(20, 0); return 0;
            }
            bool beginPointerDrag = hitKind == 3;
            if (message == WM_LBUTTONDOWN && hitKind == 2) {
                const auto* slider = HitRecordAt(header, x, y);
                if (slider) {
                    const double value = max(0.0, min(.98, header->reserved[0] / 255.0));
                    const double ratio = value / .98;
                    const int thumbX = slider->left + 5 + static_cast<int>((slider->right - slider->left - 10) * ratio);
                    beginPointerDrag = abs(x - thumbX) <= 10;
                }
            }
            if (message == WM_LBUTTONDOWN && beginPointerDrag) {
                g_pointerDragKind = hitKind; SetCapture(hwnd);
                PostDesktopAction(hitKind == 2 ? 104 : 107,
                    static_cast<LPARAM>((x & 0xFFFF) | ((y & 0xFFFF) << 16)));
                return 0;
            }
            const int rawAction = message == WM_LBUTTONDBLCLK ? 101 : 100;
            PostDesktopAction(rawAction,
                static_cast<LPARAM>((x & 0xFFFF) | ((y & 0xFFFF) << 16)));
            return 0;
        }
    }
    if (message == WM_NCDESTROY) {
        RemovePropW(hwnd, ONHARU_LISTVIEW_PROPERTY);
        RemoveWindowSubclass(hwnd, ListViewSubclass, kListSubclassId);
        if (g_iconList == hwnd) g_iconList = nullptr;
    }
    return DefSubclassProc(hwnd, message, wParam, lParam);
}

static LRESULT CALLBACK DefViewSubclass(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam,
    UINT_PTR, DWORD_PTR)
{
    if (message == WM_NOTIFY && lParam) {
        const auto* custom = reinterpret_cast<const NMCUSTOMDRAW*>(lParam);
        if (custom->hdr.hwndFrom == g_iconList && custom->hdr.code == NM_CUSTOMDRAW &&
            custom->dwDrawStage == CDDS_PREPAINT) {
            // The cached final surface is already composited over Explorer's clean
            // background, so repeated prepaint callbacks are idempotent BitBlt copies.
            DrawSharedFrame(custom->hdc);
            return CDRF_DODEFAULT;
        }
    }
    if (message == WM_NCDESTROY) {
        RemovePropW(hwnd, ONHARU_DEFVIEW_PROPERTY);
        RemoveWindowSubclass(hwnd, DefViewSubclass, kDefViewSubclassId);
        if (g_defView == hwnd) g_defView = nullptr;
    }
    return DefSubclassProc(hwnd, message, wParam, lParam);
}

static void Detach()
{
    if (g_keyboardHook) { UnhookWindowsHookEx(g_keyboardHook); g_keyboardHook = nullptr; }
    g_onharuOwnsUndo = false;
    if (g_iconList && IsWindow(g_iconList)) {
        if (g_changedDoubleBuffer) {
            SendMessageW(g_iconList, LVM_SETEXTENDEDLISTVIEWSTYLE, LVS_EX_DOUBLEBUFFER,
                g_originalListViewStyle & LVS_EX_DOUBLEBUFFER);
        }
        RemoveWindowSubclass(g_iconList, ListViewSubclass, kListSubclassId);
        RemovePropW(g_iconList, ONHARU_LISTVIEW_PROPERTY);
        RedrawWindow(g_iconList, nullptr, nullptr,
            RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW);
    }
    if (g_defView && IsWindow(g_defView)) {
        RemoveWindowSubclass(g_defView, DefViewSubclass, kDefViewSubclassId);
        RemovePropW(g_defView, ONHARU_DEFVIEW_PROPERTY);
    }
    g_iconList = nullptr;
    g_defView = nullptr;
    g_changedDoubleBuffer = false;
    CloseFrame();
}

static void Attach()
{
    if (g_iconList && IsWindow(g_iconList)) return;
    struct DesktopWindows { HWND defView; HWND iconList; } desktop = {};
    EnumWindows([](HWND top, LPARAM value) -> BOOL {
        auto result = reinterpret_cast<DesktopWindows*>(value);
        HWND defView = FindWindowExW(top, nullptr, L"SHELLDLL_DefView", nullptr);
        HWND iconList = defView ? FindWindowExW(defView, nullptr, L"SysListView32", nullptr) : nullptr;
        if (!iconList) return TRUE;
        result->defView = defView; result->iconList = iconList; return FALSE;
    }, reinterpret_cast<LPARAM>(&desktop));
    HWND defView = desktop.defView;
    HWND iconList = desktop.iconList;
    if (!iconList || !OpenFrame()) return;
    if (!SetWindowSubclass(iconList, ListViewSubclass, kListSubclassId, 0)) return;
    if (!SetWindowSubclass(defView, DefViewSubclass, kDefViewSubclassId, 0)) {
        RemoveWindowSubclass(iconList, ListViewSubclass, kListSubclassId);
        return;
    }
    g_defView = defView;
    g_iconList = iconList;
    g_keyboardHook = SetWindowsHookExW(WH_KEYBOARD, KeyboardHook, g_instance, GetCurrentThreadId());
    g_originalListViewStyle = static_cast<DWORD>(SendMessageW(g_iconList, LVM_GETEXTENDEDLISTVIEWSTYLE, 0, 0));
    g_changedDoubleBuffer = (g_originalListViewStyle & LVS_EX_DOUBLEBUFFER) == 0;
    if (g_changedDoubleBuffer) {
        SendMessageW(g_iconList, LVM_SETEXTENDEDLISTVIEWSTYLE, LVS_EX_DOUBLEBUFFER, LVS_EX_DOUBLEBUFFER);
    }
    SetPropW(g_defView, ONHARU_DEFVIEW_PROPERTY, g_instance);
    SetPropW(g_iconList, ONHARU_LISTVIEW_PROPERTY, g_instance);
    RedrawWindow(g_iconList, nullptr, nullptr,
        RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW);
}

extern "C" __declspec(dllexport) LRESULT CALLBACK OnharuHook(int code, WPARAM wParam, LPARAM lParam)
{
    if (code >= 0 && lParam) {
        const CWPSTRUCT* call = reinterpret_cast<const CWPSTRUCT*>(lParam);
        if (call->message == g_initMessage) Attach();
        else if (call->message == g_stopMessage) Detach();
    }
    return CallNextHookEx(nullptr, code, wParam, lParam);
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) {
        g_instance = instance;
        DisableThreadLibraryCalls(instance);
        g_initMessage = RegisterWindowMessageW(ONHARU_INIT_MESSAGE);
        g_stopMessage = RegisterWindowMessageW(ONHARU_STOP_MESSAGE);
        g_desktopActionMessage = kDesktopActionMessage;
    }
    return TRUE;
}
