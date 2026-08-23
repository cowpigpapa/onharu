#pragma once
#include <windows.h>

#define ONHARU_FRAME_MAPPING L"Local\\Onharu.DesktopFrame"
static constexpr DWORD ONHARU_FRAME_MAGIC = 0x3356484F; // OHV3
static constexpr DWORD ONHARU_FRAME_VERSION = 1;

#pragma pack(push, 4)
struct OnharuFrameHeader
{
    DWORD magic;
    DWORD version;
    LONG width;
    LONG height;
    LONG stride;
    LONG panelLeft;
    LONG panelTop;
    LONG panelWidth;
    LONG panelHeight;
    LONG slotBytes;
    LONG activeSlot;
    LONG generation;
    DWORD reserved[4];
};
#pragma pack(pop)

static_assert(sizeof(OnharuFrameHeader) == 64, "Shared frame header must remain stable");
