# Configure the AirAbsorptionWrapper shared library
cmake_minimum_required (VERSION 3.1)
project (AirAbsorptionWrapper)
# Set the version number
set (AirAbsorptionWrapper_VERSION_MAJOR 1)
set (AirAbsorptionWrapper_VERSION_MINOR 0)
# Set project properties
set (PROJECT_SOURCE_FILES AirAbsorptionWrapper.cpp)

# add the shared library
add_library (AirAbsorptionWrapper SHARED ${PROJECT_SOURCE_FILES})

target_link_libraries(AirAbsorptionWrapper ${AirAbsorptionProxy_LIB})
IF (WIN32)
  install (TARGETS AirAbsorptionWrapper RUNTIME DESTINATION ${NATIVE_ASSETS_DIR_X86_64})
#ELSEIF (APPLE)
#  install (TARGETS AirAbsorptionWrapper LIBRARY DESTINATION ${NATIVE_ASSETS_DIR_X86_64} RENAME "libAirAbsorptionWrapper.bundle")
ELSE()
  install (TARGETS AirAbsorptionWrapper LIBRARY DESTINATION ${NATIVE_ASSETS_DIR_X86_64})
ENDIF(WIN32)

IF(MSVC)
  INSTALL ( FILES ${PROJECT_BINARY_DIR}/Debug/AirAbsorptionWrapper.pdb
    DESTINATION ${NATIVE_ASSETS_DIR_X86_64}
    CONFIGURATIONS Debug
    )
ENDIF(MSVC)