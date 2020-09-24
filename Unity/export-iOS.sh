# /bin/bash

set -eu

UNITY_PATH="/Applications/Unity/Hub/Editor/2019.4.7f1/Unity.app/Contents/MacOS/Unity"
UNITY_PROJECT_PATH="/Users/peak/Desktop/Unity Demo"
XCODE_PROJECT_PATH="${UNITY_PROJECT_PATH}/ExportProject/iOS"

$UNITY_PATH -projectPath "$UNITY_PROJECT_PATH" -executeMethod BuildIOS.BuildForiOS -xcodeProject "${XCODE_PROJECT_PATH}"