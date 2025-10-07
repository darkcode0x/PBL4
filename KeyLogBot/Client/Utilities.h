#pragma once
#include <string>
using namespace std;

string GetCurrentTimestamp();
string EscapeJsonString(const string& input);
string CreateJsonPacket(const string& messageType, const string& payloadType, const string& content);

