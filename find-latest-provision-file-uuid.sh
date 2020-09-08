#! /bin/bash

# Convert xxx.mobileprovision file's expire date to timestamp
# Expire date format like: Thu Aug 05 09:46:12 CST 2021
# Return value like: 1234567890
function convert_timeformat()
{
	if [[ -z $1 ]]; then
		echo "[Error] No arguments supplied. First argument must be set date string."
		exit -1
	fi

	if [[ $(echo  $1 | awk -F " " '{print NF}') != 6 ]]; then
		echo "[Error] Input time format error: $1"
		exit -1
	fi

	year=$(echo  $1 | awk -F " " '{print $6}')
	month=$(echo $1 | awk -F " " '{print $2}')
	day=$(echo   $1 | awk -F " " '{print $3}')
	time=$(echo  $1 | awk -F " " '{print $4}')
	
	case ${month} in
		"Jan" )
			month=01
			;;
		"Feb" )
			month=02
			;;
		"Mar" )
			month=03
			;;
		"Apr" )
			month=04
			;;
		"May" )
			month=05
			;;
		"Jun" )
			month=06
			;;
		"Jul" )
			month=07
			;;
		"Aug" )
			month=08
			;;
		"Sep" )
			month=09
			;;
		"Oct" )
			month=10
			;;
		"Nov" )
			month=11
			;;
		"Dec" )
			month=12
			;;
	esac

	time_string="${year}-${month}-${day} ${time}"
	
	# convert time string to timestamp
	echo "$(date -j -f "%Y-%m-%d %H:%M:%S" "${time_string}" "+%s")"
}

function read_mobileprovision_info()
{
	if [[ -z "$1" ]]; then
		echo "[Error] Arguments error. First argument must be set as mobileprovision file path."
		exit -1
	fi

	if [[ -z "$2" ]]; then
		echo "[Error] Arguments error. Second argument must be set as mobileprovision file name."
		exit -1
	fi

	if [[ ! -e "$1" ]]; then
		echo "[Error] mobileprovision file not exist: $1"
		exit -1
	fi

	path_to_mobileprovision=$1
	query_file_name=$2

	# bundleID=`/usr/libexec/PlistBuddy -c 'Print :Entitlements:application-identifier' /dev/stdin <<< $(security cms -D -i "${path_to_mobileprovision}")`
	name=$(/usr/libexec/PlistBuddy -c 'Print :Name' /dev/stdin <<< $(security cms -D -i "${path_to_mobileprovision}"))
	uuid=$(/usr/libexec/PlistBuddy -c 'Print :UUID' /dev/stdin <<< $(security cms -D -i "${path_to_mobileprovision}"))
	expireDate=$(/usr/libexec/PlistBuddy -c 'Print :ExpirationDate' /dev/stdin <<< $(security cms -D -i "${path_to_mobileprovision}"))
	
	if [[ "${name}" == "${query_file_name}" ]]; then
		expireTimestamp=$(convert_timeformat "${expireDate}")
		# echo "\nfile name: ${query_file_name} : ${name}"
		# echo "compare ${expireTimestamp} vs ${provision_file_expire_time}"
		if [[ ${expireTimestamp} > ${provision_file_expire_time} ]]; then
			provision_file_expire_time=${expireTimestamp}
			provision_file_uuid=${uuid}
		fi
	fi
}

# Find mobileprovision file's last uuid according to the name.
# Usage: find_latest_uuid "example_file_name"
function find_latest_uuid()
{
	if [[ -z $1 ]]; then
		echo "[Error] No arguments supplied. First argument must be set mobileprovision name."
		exit -1
	fi

	provision_dir=~/Library/MobileDevice/"Provisioning Profiles"
	
	if [[ ! -d "${provision_dir}" ]]; then
		echo "[Error] Directory not exist: ${provision_dir}"
		exit -1
	fi

	local provision_file_expire_time=0
	local provision_file_uuid="null"
	for file in $(ls "${provision_dir}"); do
		read_mobileprovision_info "${provision_dir}/$file" "$1"
	done

	if [[ ${provision_file_uuid} == "null" ]]; then
		echo "[Error] Not exist mobileprovision file which name is: $1"
		exit -1
	fi
	
	echo "mobileprovision file: $1 , lasted uuid: ${provision_file_uuid}"
	echo "${provision_file_uuid}"
}

provision_file_name="bj_mp_dev"
uuid=$(find_latest_uuid "${provision_file_name}")
echo "$uuid"

