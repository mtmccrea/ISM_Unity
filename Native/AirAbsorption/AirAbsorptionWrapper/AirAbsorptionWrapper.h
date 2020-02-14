#pragma once


#include <cmath>
#include <math.h>
#include <stddef.h>
#include <stdlib.h>
#include <string.h>
#include "rtwtypes.h"
#include "airAbsorptionProxy_types.h"

// Copied from AudioPluginInterface
#if defined(WIN32) || defined(_WIN32) || defined(__WIN32__) || defined(_WIN64)
#   define UNITY_WIN 1
#elif defined(__MACH__) || defined(__APPLE__)
#   define UNITY_OSX 1
#elif defined(__ANDROID__)
#   define UNITY_ANDROID 1
#elif defined(__linux__)
#   define UNITY_LINUX 1
#elif defined(__PS3__)
#   define UNITY_PS3 1
#elif defined(__SPU__)
#   define UNITY_SPU 1
#endif
// Attribute to make function be exported from a plugin
#if UNITY_WIN
#define UNITY_EXPORT_API __declspec(dllexport)
#else
#define UNITY_EXPORT_API
#endif


extern "C" UNITY_EXPORT_API int AirAbsorption_Initialize();
extern "C" UNITY_EXPORT_API int AirAbsorption_SetFs(unsigned int fs);
extern "C" UNITY_EXPORT_API int AirAbsorption_Apply(float* ir_in, int in_count, float* ir_out, int* out_count);
extern "C" UNITY_EXPORT_API int AirAbsorption_Terminate();
