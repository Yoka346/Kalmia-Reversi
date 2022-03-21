#pragma once

template<class T, void (*func)()>
class InitializeCallback
{
public:
	InitializeCallback() { func(); };
};

#define INIT_CALLBACK(t, f) InitializeCallback<t, f> init_callback