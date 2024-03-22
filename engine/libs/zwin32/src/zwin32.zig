pub const w32 = @import("w32.zig");
pub const wasapi = @import("wasapi.zig");
pub const mf = @import("mf.zig");
pub const xaudio2 = @import("xaudio2.zig");
pub const xaudio2fx = @import("xaudio2fx.zig");

test {
    std.testing.refAllDeclsRecursive(@This());
}

const HRESULT = w32.HRESULT;
const S_OK = w32.S_OK;

const std = @import("std");
const panic = std.debug.panic;
const assert = std.debug.assert;

// TODO: Handle more error codes from https://docs.microsoft.com/en-us/windows/win32/com/com-error-codes-10
pub const HResultError =
    w32.MiscError || w32.Error || wasapi.Error;

pub fn hrPanic(err: HResultError) noreturn {
    panic(
        "HRESULT error detected (0x{x}, {}).",
        .{ @as(c_ulong, @bitCast(errorToHRESULT(err))), err },
    );
}

pub inline fn hrPanicOnFail(hr: HRESULT) void {
    if (hr != S_OK) {
        hrPanic(hrToError(hr));
    }
}

pub inline fn hrErrorOnFail(hr: HRESULT) HResultError!void {
    if (hr != S_OK) {
        return hrToError(hr);
    }
}

pub fn hrToError(hr: HRESULT) HResultError {
    assert(hr != S_OK);
    return switch (hr) {
        //
        w32.E_UNEXPECTED => w32.Error.UNEXPECTED,
        w32.E_NOTIMPL => w32.Error.NOTIMPL,
        w32.E_OUTOFMEMORY => w32.Error.OUTOFMEMORY,
        w32.E_INVALIDARG => w32.Error.INVALIDARG,
        w32.E_POINTER => w32.Error.POINTER,
        w32.E_HANDLE => w32.Error.HANDLE,
        w32.E_ABORT => w32.Error.ABORT,
        w32.E_FAIL => w32.Error.FAIL,
        w32.E_ACCESSDENIED => w32.Error.ACCESSDENIED,
        //
        wasapi.AUDCLNT_E_NOT_INITIALIZED => wasapi.Error.NOT_INITIALIZED,
        wasapi.AUDCLNT_E_ALREADY_INITIALIZED => wasapi.Error.ALREADY_INITIALIZED,
        wasapi.AUDCLNT_E_WRONG_ENDPOINT_TYPE => wasapi.Error.WRONG_ENDPOINT_TYPE,
        wasapi.AUDCLNT_E_DEVICE_INVALIDATED => wasapi.Error.DEVICE_INVALIDATED,
        wasapi.AUDCLNT_E_NOT_STOPPED => wasapi.Error.NOT_STOPPED,
        wasapi.AUDCLNT_E_BUFFER_TOO_LARGE => wasapi.Error.BUFFER_TOO_LARGE,
        wasapi.AUDCLNT_E_OUT_OF_ORDER => wasapi.Error.OUT_OF_ORDER,
        wasapi.AUDCLNT_E_UNSUPPORTED_FORMAT => wasapi.Error.UNSUPPORTED_FORMAT,
        wasapi.AUDCLNT_E_INVALID_SIZE => wasapi.Error.INVALID_SIZE,
        wasapi.AUDCLNT_E_DEVICE_IN_USE => wasapi.Error.DEVICE_IN_USE,
        wasapi.AUDCLNT_E_BUFFER_OPERATION_PENDING => wasapi.Error.BUFFER_OPERATION_PENDING,
        wasapi.AUDCLNT_E_THREAD_NOT_REGISTERED => wasapi.Error.THREAD_NOT_REGISTERED,
        wasapi.AUDCLNT_E_EXCLUSIVE_MODE_NOT_ALLOWED => wasapi.Error.EXCLUSIVE_MODE_NOT_ALLOWED,
        wasapi.AUDCLNT_E_ENDPOINT_CREATE_FAILED => wasapi.Error.ENDPOINT_CREATE_FAILED,
        wasapi.AUDCLNT_E_SERVICE_NOT_RUNNING => wasapi.Error.SERVICE_NOT_RUNNING,
        wasapi.AUDCLNT_E_EVENTHANDLE_NOT_EXPECTED => wasapi.Error.EVENTHANDLE_NOT_EXPECTED,
        wasapi.AUDCLNT_E_EXCLUSIVE_MODE_ONLY => wasapi.Error.EXCLUSIVE_MODE_ONLY,
        wasapi.AUDCLNT_E_BUFDURATION_PERIOD_NOT_EQUAL => wasapi.Error.BUFDURATION_PERIOD_NOT_EQUAL,
        wasapi.AUDCLNT_E_EVENTHANDLE_NOT_SET => wasapi.Error.EVENTHANDLE_NOT_SET,
        wasapi.AUDCLNT_E_INCORRECT_BUFFER_SIZE => wasapi.Error.INCORRECT_BUFFER_SIZE,
        wasapi.AUDCLNT_E_BUFFER_SIZE_ERROR => wasapi.Error.BUFFER_SIZE_ERROR,
        wasapi.AUDCLNT_E_CPUUSAGE_EXCEEDED => wasapi.Error.CPUUSAGE_EXCEEDED,
        wasapi.AUDCLNT_E_BUFFER_ERROR => wasapi.Error.BUFFER_ERROR,
        wasapi.AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED => wasapi.Error.BUFFER_SIZE_NOT_ALIGNED,
        wasapi.AUDCLNT_E_INVALID_DEVICE_PERIOD => wasapi.Error.INVALID_DEVICE_PERIOD,
        //
        w32.E_FILE_NOT_FOUND => w32.MiscError.E_FILE_NOT_FOUND,
        w32.S_FALSE => w32.MiscError.S_FALSE,
        // treat unknown error return codes as E_FAIL
        else => blk: {
            // std.log.debug("HRESULT error 0x{x} not recognized treating as E_FAIL.", .{@as(c_ulong, @bitCast(hr))});
            break :blk w32.Error.FAIL;
        },
    };
}

pub fn errorToHRESULT(err: HResultError) HRESULT {
    return switch (err) {
        w32.Error.UNEXPECTED => w32.E_UNEXPECTED,
        w32.Error.NOTIMPL => w32.E_NOTIMPL,
        w32.Error.OUTOFMEMORY => w32.E_OUTOFMEMORY,
        w32.Error.INVALIDARG => w32.E_INVALIDARG,
        w32.Error.POINTER => w32.E_POINTER,
        w32.Error.HANDLE => w32.E_HANDLE,
        w32.Error.ABORT => w32.E_ABORT,
        w32.Error.FAIL => w32.E_FAIL,
        w32.Error.ACCESSDENIED => w32.E_ACCESSDENIED,
        //
        wasapi.Error.NOT_INITIALIZED => wasapi.AUDCLNT_E_NOT_INITIALIZED,
        wasapi.Error.ALREADY_INITIALIZED => wasapi.AUDCLNT_E_ALREADY_INITIALIZED,
        wasapi.Error.WRONG_ENDPOINT_TYPE => wasapi.AUDCLNT_E_WRONG_ENDPOINT_TYPE,
        wasapi.Error.DEVICE_INVALIDATED => wasapi.AUDCLNT_E_DEVICE_INVALIDATED,
        wasapi.Error.NOT_STOPPED => wasapi.AUDCLNT_E_NOT_STOPPED,
        wasapi.Error.BUFFER_TOO_LARGE => wasapi.AUDCLNT_E_BUFFER_TOO_LARGE,
        wasapi.Error.OUT_OF_ORDER => wasapi.AUDCLNT_E_OUT_OF_ORDER,
        wasapi.Error.UNSUPPORTED_FORMAT => wasapi.AUDCLNT_E_UNSUPPORTED_FORMAT,
        wasapi.Error.INVALID_SIZE => wasapi.AUDCLNT_E_INVALID_SIZE,
        wasapi.Error.DEVICE_IN_USE => wasapi.AUDCLNT_E_DEVICE_IN_USE,
        wasapi.Error.BUFFER_OPERATION_PENDING => wasapi.AUDCLNT_E_BUFFER_OPERATION_PENDING,
        wasapi.Error.THREAD_NOT_REGISTERED => wasapi.AUDCLNT_E_THREAD_NOT_REGISTERED,
        wasapi.Error.EXCLUSIVE_MODE_NOT_ALLOWED => wasapi.AUDCLNT_E_EXCLUSIVE_MODE_NOT_ALLOWED,
        wasapi.Error.ENDPOINT_CREATE_FAILED => wasapi.AUDCLNT_E_ENDPOINT_CREATE_FAILED,
        wasapi.Error.SERVICE_NOT_RUNNING => wasapi.AUDCLNT_E_SERVICE_NOT_RUNNING,
        wasapi.Error.EVENTHANDLE_NOT_EXPECTED => wasapi.AUDCLNT_E_EVENTHANDLE_NOT_EXPECTED,
        wasapi.Error.EXCLUSIVE_MODE_ONLY => wasapi.AUDCLNT_E_EXCLUSIVE_MODE_ONLY,
        wasapi.Error.BUFDURATION_PERIOD_NOT_EQUAL => wasapi.AUDCLNT_E_BUFDURATION_PERIOD_NOT_EQUAL,
        wasapi.Error.EVENTHANDLE_NOT_SET => wasapi.AUDCLNT_E_EVENTHANDLE_NOT_SET,
        wasapi.Error.INCORRECT_BUFFER_SIZE => wasapi.AUDCLNT_E_INCORRECT_BUFFER_SIZE,
        wasapi.Error.BUFFER_SIZE_ERROR => wasapi.AUDCLNT_E_BUFFER_SIZE_ERROR,
        wasapi.Error.CPUUSAGE_EXCEEDED => wasapi.AUDCLNT_E_CPUUSAGE_EXCEEDED,
        wasapi.Error.BUFFER_ERROR => wasapi.AUDCLNT_E_BUFFER_ERROR,
        wasapi.Error.BUFFER_SIZE_NOT_ALIGNED => wasapi.AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED,
        wasapi.Error.INVALID_DEVICE_PERIOD => wasapi.AUDCLNT_E_INVALID_DEVICE_PERIOD,
        //
        w32.MiscError.E_FILE_NOT_FOUND => w32.E_FILE_NOT_FOUND,
        w32.MiscError.S_FALSE => w32.S_FALSE,
    };
}
