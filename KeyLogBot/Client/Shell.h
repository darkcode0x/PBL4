#pragma once
#include <windows.h>
#include <string>
#include <thread>
#include <mutex>
#include <atomic>

#include "IClient.h"
class Shell
{
public:
    Shell(IClient* client) : _client(client), _procInfo{0}, _read(true) {}
    ~Shell() { Dispose(); }

    bool CreateSession();
    bool ExecuteCommand(const std::string& commandUtf8);
    void Dispose(){ _read.store(false); DisposeProcessResources(); }

private:
    void RedirectReadThread(HANDLE pipeRead, bool isError);
    bool IsProcessAlive();
    void DisposeProcessResources();

    IClient* _client;
    PROCESS_INFORMATION _procInfo;
    HANDLE _hChildStd_IN_Rd = nullptr;
    HANDLE _hChildStd_IN_Wr = nullptr;
    HANDLE _hChildStd_OUT_Rd = nullptr;
    HANDLE _hChildStd_OUT_Wr = nullptr;
    HANDLE _hChildStd_ERR_Rd = nullptr;
    HANDLE _hChildStd_ERR_Wr = nullptr;

    std::thread _outThread;
    std::thread _errThread;
    std::mutex _stdinMutex;
    std::atomic<bool> _read;
};
