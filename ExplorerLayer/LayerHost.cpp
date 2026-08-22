#include <windows.h>
#include <fstream>
#include <iostream>
#include <string>
#include "LayerShared.h"

static constexpr wchar_t kStopEventName[] = L"Local\\OnharuV3.LayerHost.Stop";
static HANDLE g_stopEvent;

struct DesktopWindows { HWND defView = nullptr; HWND iconList = nullptr; };

static BOOL CALLBACK FindDesktopWindowsCallback(HWND top, LPARAM value)
{
    auto result = reinterpret_cast<DesktopWindows*>(value);
    HWND defView = FindWindowExW(top, nullptr, L"SHELLDLL_DefView", nullptr);
    if (!defView) return TRUE;
    HWND iconList = FindWindowExW(defView, nullptr, L"SysListView32", nullptr);
    if (!iconList) return TRUE;
    result->defView = defView; result->iconList = iconList;
    return FALSE;
}

static DesktopWindows FindDesktopWindows()
{
    DesktopWindows result;
    EnumWindows(FindDesktopWindowsCallback, reinterpret_cast<LPARAM>(&result));
    return result;
}

static void SendStop(HWND progman)
{
    if (!progman) return;
    DWORD_PTR ignored = 0;
    SendMessageTimeoutW(progman, RegisterWindowMessageW(ONHARU_STOP_MESSAGE), 0, 0,
        SMTO_ABORTIFHUNG, 2000, &ignored);
}

static BOOL WINAPI ConsoleHandler(DWORD type)
{
    if (type == CTRL_C_EVENT || type == CTRL_CLOSE_EVENT) {
        if (g_stopEvent) SetEvent(g_stopEvent);
        auto desktop = FindDesktopWindows();
        SendStop(desktop.defView ? desktop.defView : FindWindowW(L"Progman", nullptr));
        return TRUE;
    }
    return FALSE;
}

int wmain(int argc, wchar_t** argv)
{
    wchar_t localAppData[32768] = {};
    std::wstring logPath = L"layer-host.log";
    if (GetEnvironmentVariableW(L"LOCALAPPDATA", localAppData, ARRAYSIZE(localAppData)) > 0) {
        std::wstring appData(localAppData);
        appData += L"\\OnharuV3"; CreateDirectoryW(appData.c_str(), nullptr);
        appData += L"\\logs"; CreateDirectoryW(appData.c_str(), nullptr);
        logPath = appData + L"\\layer-host.log";
    }
    std::wofstream log(logPath, std::ios::app);
    HWND progman = FindWindowW(L"Progman", nullptr);
    if (!progman) { std::wcerr << L"Progman not found.\n"; return 1; }
    if (argc > 1 && std::wstring(argv[1]) == L"--stop") {
        HANDLE stopEvent = OpenEventW(EVENT_MODIFY_STATE, FALSE, kStopEventName);
        if (stopEvent) { SetEvent(stopEvent); CloseHandle(stopEvent); }
        auto desktop = FindDesktopWindows();
        SendStop(desktop.defView ? desktop.defView : progman);
        std::wcout << L"Prototype stopped.\n";
        return 0;
    }

    g_stopEvent = CreateEventW(nullptr, TRUE, FALSE, kStopEventName);
    if (!g_stopEvent) return 4;
    wchar_t executablePath[32768] = {};
    GetModuleFileNameW(nullptr, executablePath, ARRAYSIZE(executablePath));
    std::wstring dllPath(executablePath);
    dllPath.erase(dllPath.find_last_of(L"\\/") + 1);
    dllPath += L"OnharuV3.DesktopHook.dll";
    HMODULE dll = LoadLibraryW(dllPath.c_str());
    if (!dll) { std::wcerr << L"DLL load failed: " << GetLastError() << L"\n"; return 2; }
    HOOKPROC procedure = reinterpret_cast<HOOKPROC>(GetProcAddress(dll, "OnharuHook"));
    if (!procedure) { FreeLibrary(dll); return 3; }
    SetConsoleCtrlHandler(ConsoleHandler, TRUE);
    bool firstAttach = true;
    for (;;)
    {
        progman = FindWindowW(L"Progman", nullptr);
        if (WaitForSingleObject(g_stopEvent, 0) == WAIT_OBJECT_0) break;
        if (!progman) { WaitForSingleObject(g_stopEvent, 1000); continue; }
        auto desktop = FindDesktopWindows();
        if (!desktop.defView || !desktop.iconList) { WaitForSingleObject(g_stopEvent, 1000); continue; }
        DWORD processId = 0;
        DWORD threadId = GetWindowThreadProcessId(desktop.defView, &processId);
        HHOOK hook = SetWindowsHookExW(WH_CALLWNDPROC, procedure, dll, threadId);
        if (!hook) { log << L"hook-install-error=" << GetLastError() << L"\n"; WaitForSingleObject(g_stopEvent, 1000); continue; }
        DWORD_PTR ignored = 0;
        SendMessageTimeoutW(desktop.defView, RegisterWindowMessageW(ONHARU_INIT_MESSAGE), 0, 0,
            SMTO_ABORTIFHUNG, 3000, &ignored);
        HWND defView = desktop.defView;
        HWND iconList = desktop.iconList;
        bool attached = defView && iconList && GetPropW(defView, ONHARU_DEFVIEW_PROPERTY) && GetPropW(iconList, ONHARU_LISTVIEW_PROPERTY);
        log << L"pid=" << processId << L" attached=" << attached << L" defview=0x" << std::hex
            << reinterpret_cast<UINT_PTR>(defView) << L" listview=0x" << reinterpret_cast<UINT_PTR>(iconList) << std::dec << L"\n";
        log.flush();
        if (firstAttach) {
            std::wcout << (attached ? L"Calendar paint hook attached. Monitoring Explorer.\n" : L"Calendar paint hook was not attached; retrying.\n");
            firstAttach = false;
        }
        while (attached && IsWindow(defView) && IsWindow(iconList) && GetPropW(iconList, ONHARU_LISTVIEW_PROPERTY) &&
            WaitForSingleObject(g_stopEvent, 500) == WAIT_TIMEOUT) {}
        SendStop(defView);
        UnhookWindowsHookEx(hook);
        if (WaitForSingleObject(g_stopEvent, 750) == WAIT_OBJECT_0) break;
    }
    CloseHandle(g_stopEvent); g_stopEvent = nullptr;
    FreeLibrary(dll);
    return 0;
}
