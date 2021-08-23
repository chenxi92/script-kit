#!/bin/bash

function getUDID {
	udid=($(system_profiler SPUSBDataType | grep -A 11 -w "iPad\|iPhone\|iPod\|AppleTV" | grep "Serial Number" | awk '{ print $3 }'))
	if [ -z $udid ]; then
		echo "No device detected. Please ensure an iOS device is plugged in."
		exit 1
	else
		for id in "${udid[@]}"; do
			if [ ${#id} -eq 24 ]; then
				id=`echo $id | sed 's/\(........\)/\1-/'` # Add proper formatting for new-style UDIDs
			fi 
			echo $id | pbcopy # Copy the UDID to the clipboard since we probably want to paste it somewhere.
			echo "UDID: $id"
		done
	fi
}

getUDID