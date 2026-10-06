# A banknote validator on an ESP32, using the SmileySecure ESP-IDF component.
# SPDX-License-Identifier: LGPL-3.0-or-later
cmake_minimum_required(VERSION 3.16)

# In your own project, add the component through idf_component.yml instead; see cpp/README.md.
set(EXTRA_COMPONENT_DIRS "${CMAKE_CURRENT_LIST_DIR}/../../idf")

include($ENV{IDF_PATH}/tools/cmake/project.cmake)
project(esp32_validator)
