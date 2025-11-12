#pragma once
#include <string>
class IClient
{
public:
    virtual ~IClient() {}
    virtual void Send(const std::string& outputHex, bool isError) = 0;
};
