cmake_minimum_required (VERSION 3.1)
project (AudioPluginISM)
# generate install path
set (NATIVE_ASSETS_DIR_X86_64 ${CMAKE_CURRENT_SOURCE_DIR}/Assets/Plugins/x86_64)
# Use C++11
set (CMAKE_CXX_STANDARD 11)
# Run cmake in subdirectories below
add_subdirectory("${CMAKE_CURRENT_SOURCE_DIR}/Native/AudioPluginVADemo")
add_subdirectory("${CMAKE_CURRENT_SOURCE_DIR}/Native/AirAbsorption")

