#pragma once
#include <string>

std::string GetCurrentTimestamp();
std::string EscapeJsonString(const std::string& input);
std::string CreateJsonPacket(const std::string& messageType, const std::string& payloadType, const std::string& content);

