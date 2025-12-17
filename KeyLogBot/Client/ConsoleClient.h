#pragma once
#include "IClient.h"
#include "Botnet.h"
#include <mutex>
#include <iomanip>
#include <iostream>
#include <sstream>
class ConsoleClient : public IClient {
public:
    ConsoleClient(){};
    void Send(const std::string& rawBytes, bool isError) override {
        // Send raw bytes to queue without logging to console
        EnqueueSend(rawBytes);
    }
private:
    std::mutex _mtx;
};