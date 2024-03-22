const builtin = @import("builtin");
const native_arch = builtin.cpu.arch;
const std = @import("std");

pub extern "ole32" fn CoInitializeEx(pvReserved: ?LPVOID, dwCoInit: DWORD) callconv(WINAPI) HRESULT;
pub extern "ole32" fn CoUninitialize() callconv(WINAPI) void;
pub extern "ole32" fn CoTaskMemAlloc(size: SIZE_T) callconv(WINAPI) ?LPVOID;
pub extern "ole32" fn CoTaskMemFree(pv: LPVOID) callconv(WINAPI) void;

pub const COINIT_APARTMENTTHREADED = 0x2;
pub const COINIT_MULTITHREADED = 0x3;
pub const COINIT_DISABLE_OLE1DDE = 0x4;
pub const COINIT_SPEED_OVER_MEMORY = 0x8;

pub const WINAPI: std.builtin.CallingConvention = if (native_arch == .x86) .Stdcall else .C;
pub const BOOL = c_int;
pub const BOOLEAN = BYTE;
pub const BYTE = u8;
pub const CHAR = u8;
pub const UCHAR = u8;
pub const FLOAT = f32;
pub const HANDLE = *anyopaque;
pub const HCRYPTPROV = ULONG_PTR;
pub const ATOM = u16;
pub const HBRUSH = *opaque {};
pub const HCURSOR = *opaque {};
pub const HICON = *opaque {};
pub const HINSTANCE = *opaque {};
pub const HMENU = *opaque {};
pub const HMODULE = *opaque {};
pub const HWND = *opaque {};
pub const HDC = *opaque {};
pub const HGLRC = *opaque {};
pub const FARPROC = *opaque {};
pub const INT = c_int;
pub const LPCSTR = [*:0]const CHAR;
pub const LPCVOID = *const anyopaque;
pub const LPSTR = [*:0]CHAR;
pub const LPVOID = *anyopaque;
pub const LPWSTR = [*:0]WCHAR;
pub const LPCWSTR = [*:0]const WCHAR;
pub const PVOID = *anyopaque;
pub const PWSTR = [*:0]WCHAR;
pub const PCWSTR = [*:0]const WCHAR;
pub const SIZE_T = usize;
pub const UINT = c_uint;
pub const ULONG_PTR = usize;
pub const LONG_PTR = isize;
pub const DWORD_PTR = ULONG_PTR;
pub const WCHAR = u16;
pub const WORD = u16;
pub const DWORD = u32;
pub const DWORD64 = u64;
pub const LARGE_INTEGER = i64;
pub const ULARGE_INTEGER = u64;
pub const USHORT = u16;
pub const SHORT = i16;
pub const ULONG = u32;
pub const LONG = i32;
pub const ULONG64 = u64;
pub const ULONGLONG = u64;
pub const LONGLONG = i64;
pub const HLOCAL = HANDLE;
pub const LANGID = c_ushort;
pub const HRESULT = c_long;
pub const GUID = std.os.windows.GUID;
pub const NTSTATUS = std.os.windows.NTSTATUS;
pub const CRITICAL_SECTION = std.os.windows.CRITICAL_SECTION;

pub const WPARAM = usize;
pub const LPARAM = LONG_PTR;
pub const LRESULT = LONG_PTR;

pub const TRUE = 1;
pub const FALSE = 0;

pub const MAX_PATH = 260;

pub const S_OK = 0;
pub const S_FALSE = @as(HRESULT, @bitCast(@as(c_ulong, 0x00000001)));
pub const E_NOTIMPL = @as(c_long, @bitCast(@as(c_ulong, 0x80004001)));
pub const E_NOINTERFACE = @as(c_long, @bitCast(@as(c_ulong, 0x80004002)));
pub const E_POINTER = @as(c_long, @bitCast(@as(c_ulong, 0x80004003)));
pub const E_ABORT = @as(c_long, @bitCast(@as(c_ulong, 0x80004004)));
pub const E_FAIL = @as(c_long, @bitCast(@as(c_ulong, 0x80004005)));
pub const E_UNEXPECTED = @as(c_long, @bitCast(@as(c_ulong, 0x8000FFFF)));
pub const E_ACCESSDENIED = @as(c_long, @bitCast(@as(c_ulong, 0x80070005)));
pub const E_HANDLE = @as(c_long, @bitCast(@as(c_ulong, 0x80070006)));
pub const E_OUTOFMEMORY = @as(c_long, @bitCast(@as(c_ulong, 0x8007000E)));
pub const E_INVALIDARG = @as(c_long, @bitCast(@as(c_ulong, 0x80070057)));
pub const E_FILE_NOT_FOUND = @as(HRESULT, @bitCast(@as(c_ulong, 0x80070002)));

pub const Error = error{
    UNEXPECTED,
    NOTIMPL,
    OUTOFMEMORY,
    INVALIDARG,
    POINTER,
    HANDLE,
    ABORT,
    FAIL,
    ACCESSDENIED,
};

pub const MiscError = error{
    E_FILE_NOT_FOUND,
    S_FALSE,
};

pub const ERROR_SUCCESS = @as(LONG, 0);
pub const ERROR_DEVICE_NOT_CONNECTED = @as(LONG, 1167);
pub const ERROR_EMPTY = @as(LONG, 4306);

pub const SEVERITY_SUCCESS = 0;
pub const SEVERITY_ERROR = 1;

pub fn MAKE_HRESULT(severity: LONG, facility: LONG, value: LONG) HRESULT {
    return @as(HRESULT, (severity << 31) | (facility << 16) | value);
}

pub const GENERIC_READ = 0x80000000;
pub const GENERIC_WRITE = 0x40000000;
pub const GENERIC_EXECUTE = 0x20000000;
pub const GENERIC_ALL = 0x10000000;

pub const CW_USEDEFAULT = @as(i32, @bitCast(@as(u32, 0x80000000)));

pub const SECURITY_ATTRIBUTES = extern struct {
    nLength: DWORD,
    lpSecurityDescriptor: ?*anyopaque,
    bInheritHandle: BOOL,
};

pub extern "kernel32" fn GetModuleHandleA(lpModuleName: ?LPCSTR) callconv(WINAPI) ?HMODULE;

pub extern "kernel32" fn LoadLibraryA(lpLibFileName: LPCSTR) callconv(WINAPI) ?HMODULE;

pub extern "kernel32" fn GetProcAddress(hModule: HMODULE, lpProcName: LPCSTR) callconv(WINAPI) ?FARPROC;

pub extern "kernel32" fn FreeLibrary(hModule: HMODULE) callconv(WINAPI) BOOL;

pub extern "kernel32" fn ExitProcess(exit_code: UINT) callconv(WINAPI) noreturn;

pub extern "kernel32" fn CloseHandle(hObject: HANDLE) callconv(WINAPI) BOOL;

pub const PTHREAD_START_ROUTINE = *const fn (LPVOID) callconv(.C) DWORD;
pub const LPTHREAD_START_ROUTINE = PTHREAD_START_ROUTINE;

pub extern "kernel32" fn CreateThread(
    lpThreadAttributes: ?*SECURITY_ATTRIBUTES,
    dwStackSize: SIZE_T,
    lpStartAddress: LPTHREAD_START_ROUTINE,
    lpParameter: ?LPVOID,
    dwCreationFlags: DWORD,
    lpThreadId: ?*DWORD,
) callconv(WINAPI) ?HANDLE;

pub const EVENT_ALL_ACCESS = 0x1F0003;

pub extern "kernel32" fn CreateEventExA(
    lpEventAttributes: ?*SECURITY_ATTRIBUTES,
    lpName: LPCSTR,
    dwFlags: DWORD,
    dwDesiredAccess: DWORD,
) callconv(WINAPI) ?HANDLE;

pub const INFINITE = 4294967295;

pub extern "kernel32" fn WaitForSingleObject(hHandle: HANDLE, dwMilliseconds: DWORD) callconv(WINAPI) DWORD;

pub extern "kernel32" fn InitializeCriticalSection(lpCriticalSection: *CRITICAL_SECTION) callconv(WINAPI) void;
pub extern "kernel32" fn EnterCriticalSection(lpCriticalSection: *CRITICAL_SECTION) callconv(WINAPI) void;
pub extern "kernel32" fn LeaveCriticalSection(lpCriticalSection: *CRITICAL_SECTION) callconv(WINAPI) void;
pub extern "kernel32" fn DeleteCriticalSection(lpCriticalSection: *CRITICAL_SECTION) callconv(WINAPI) void;

pub extern "kernel32" fn Sleep(dwMilliseconds: DWORD) void;

pub extern "ntdll" fn RtlGetVersion(lpVersionInformation: *OSVERSIONINFOW) callconv(WINAPI) NTSTATUS;

pub const OSVERSIONINFOW = extern struct {
    dwOSVersionInfoSize: ULONG,
    dwMajorVersion: ULONG,
    dwMinorVersion: ULONG,
    dwBuildNumber: ULONG,
    dwPlatformId: ULONG,
    szCSDVersion: [128]WCHAR,
};

pub const INT8 = i8;
pub const UINT8 = u8;
pub const UINT16 = c_ushort;
pub const UINT32 = c_uint;
pub const UINT64 = c_ulonglong;
pub const HMONITOR = HANDLE;
pub const REFERENCE_TIME = c_longlong;
pub const LUID = extern struct {
    LowPart: DWORD,
    HighPart: LONG,
};

pub const VT_UI4 = 19;
pub const VT_I8 = 20;
pub const VT_UI8 = 21;
pub const VT_INT = 22;
pub const VT_UINT = 23;

pub const VARTYPE = u16;

pub const PROPVARIANT = extern struct {
    vt: VARTYPE,
    wReserved1: WORD = 0,
    wReserved2: WORD = 0,
    wReserved3: WORD = 0,
    u: extern union {
        intVal: i32,
        uintVal: u32,
        hVal: i64,
    },
    decVal: u64 = 0,
};
comptime {
    std.debug.assert(@sizeOf(PROPVARIANT) == 24);
}

pub const WHEEL_DELTA = 120;

pub inline fn GET_WHEEL_DELTA_WPARAM(wparam: WPARAM) i16 {
    return @as(i16, @bitCast(@as(u16, @intCast((wparam >> 16) & 0xffff))));
}

pub inline fn GET_X_LPARAM(lparam: LPARAM) i32 {
    return @as(i32, @intCast(@as(i16, @bitCast(@as(u16, @intCast(lparam & 0xffff))))));
}

pub inline fn GET_Y_LPARAM(lparam: LPARAM) i32 {
    return @as(i32, @intCast(@as(i16, @bitCast(@as(u16, @intCast((lparam >> 16) & 0xffff))))));
}

pub inline fn LOWORD(dword: DWORD) WORD {
    return @as(WORD, @bitCast(@as(u16, @intCast(dword & 0xffff))));
}

pub inline fn HIWORD(dword: DWORD) WORD {
    return @as(WORD, @bitCast(@as(u16, @intCast((dword >> 16) & 0xffff))));
}

pub const IID_IUnknown = GUID.parse("{00000000-0000-0000-C000-000000000046}");
pub const IUnknown = extern struct {
    __v: *const VTable,

    pub usingnamespace Methods(@This());

    pub fn Methods(comptime T: type) type {
        return extern struct {
            pub inline fn QueryInterface(self: *T, guid: *const GUID, outobj: ?*?*anyopaque) HRESULT {
                return @as(*const IUnknown.VTable, @ptrCast(self.__v))
                    .QueryInterface(@as(*IUnknown, @ptrCast(self)), guid, outobj);
            }
            pub inline fn AddRef(self: *T) ULONG {
                return @as(*const IUnknown.VTable, @ptrCast(self.__v)).AddRef(@as(*IUnknown, @ptrCast(self)));
            }
            pub inline fn Release(self: *T) ULONG {
                return @as(*const IUnknown.VTable, @ptrCast(self.__v)).Release(@as(*IUnknown, @ptrCast(self)));
            }
        };
    }

    pub const VTable = extern struct {
        QueryInterface: *const fn (*IUnknown, *const GUID, ?*?*anyopaque) callconv(WINAPI) HRESULT,
        AddRef: *const fn (*IUnknown) callconv(WINAPI) ULONG,
        Release: *const fn (*IUnknown) callconv(WINAPI) ULONG,
    };
};

pub extern "kernel32" fn ExitThread(DWORD) callconv(WINAPI) void;
pub extern "kernel32" fn TerminateThread(HANDLE, DWORD) callconv(WINAPI) BOOL;

pub const CLSCTX_INPROC_SERVER = 0x1;

pub extern "ole32" fn CoCreateInstance(
    rclsid: *const GUID,
    pUnkOuter: ?*IUnknown,
    dwClsContext: DWORD,
    riid: *const GUID,
    ppv: *?*anyopaque,
) callconv(WINAPI) HRESULT;

pub const GUID_NULL = GUID.parse("{00000000-0000-0000-0000-000000000000}");
