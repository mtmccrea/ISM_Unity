# Configure the AudioPluginISMDemo shared library
cmake_minimum_required (VERSION 3.1)
message (STATUS "Adding AudioPluginVADemo to the build.")
project (AudioPluginVADemo)
# Set the version number
set (AudioPluginISMDemo_VERSION_MAJOR 1)
set (AudioPluginISMDemo_VERSION_MINOR 0)
# Set project properties
set (PROJECT_SOURCE_FILES AudioPluginUtil.cpp Plugin_ConvolutionReverb.cpp)
# add the shared library
add_library (AudioPluginVADemo SHARED ${PROJECT_SOURCE_FILES})
IF (WIN32)
  install(TARGETS AudioPluginVADemo RUNTIME DESTINATION ${NATIVE_ASSETS_DIR_X86_64})
ELSE()
  install(TARGETS AudioPluginVADemo LIBRARY DESTINATION ${NATIVE_ASSETS_DIR_X86_64})
ENDIF(WIN32)
IF(MSVC)
  install ( FILES ${PROJECT_BINARY_DIR}/Debug/AudioPluginVADemo.pdb
    DESTINATION ${NATIVE_ASSETS_DIR_X86_64}
    CONFIGURATIONS Debug
    )
ENDIF(MSVC)
