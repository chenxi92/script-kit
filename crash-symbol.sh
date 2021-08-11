#! /bin/bash

set -eu

export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer

# use this command line to find path:
# find /Applications/Xcode.App -name symbolicatecrash -type f
symbolicatecrash="/Applications/Xcode.app/Contents/Developer/Platforms/MacOSX.platform/Developer/iOSSupport/Library/PrivateFrameworks/DVTFoundation.framework/Versions/A/Resources/symbolicatecrash"

crash_file=""
dsym_file=""
output_file="symbolized-crash.log"

function detect_file()
{
	local file=$(find ./ -name *.crash)
	if [[ -z ${file} ]]; then
		echo "not found crash file: $(pwd)"
		exit 1
	fi
	crash_file=${file}
	echo "\nPrint carsh file UUID:"
	grep "arm" ${crash_file} | head -n 1

	file=$(find ./ -name *.dSYM)
	if [[ -z ${file} ]]; then
		echo "not found dsym file: $(pwd)"
		exit 1
	fi
	dsym_file=${file}

	file=$(find ${dsym_file}/Contents/Resources/DWARF -type f)
	if [[ ! -z ${file} ]]; then
		echo "\nPrint dSYM File UUID:"
		dwarfdump --uuid ${file}
	fi
}

detect_file


${symbolicatecrash} ${crash_file} ${dsym_file} > ${output_file}
