#!/bin/sh
# 
# Build android apk/aab file and upload to server, or export android gradle project.
#
# 
# Usage:
# sh ./build_client_android.sh -buildType 1 -bundleVersion 0.0.1 -bundleVersionCode 1


WORK_DIR="<work-directory>"
GIT_PROJECT_PATH="${WORK_DIR}/<git-project-path>"
CLIENT_PROJECT_PATH="${GIT_PROJECT_PATH}/<unity-project-path>"
UNITY_EXE_PATH="/Applications/Unity/Hub/Editor/2020.3.18f1/Unity.app/Contents/MacOS/Unity"


buildType="1"         # 1: build apk; 2: build aab; 3: build android project
bundleVersion="0.0.1" # Application bundle version
bundleVersionCode="1" # Android bundle version code


function log() {
	echo "`date '+%F %T'` $@"
}

function logError() {
	echo "`date '+%F %T'` $@"
	exit 1
}

function check_parameter() {
	log "WORK_DIR            = ${WORK_DIR}"
	log "CLIENT_PROJECT_PATH = $CLIENT_PROJECT_PATH"
	log "UNITY_EXE_PATH      = ${UNITY_EXE_PATH}"
	
	log "buildType = ${buildType}"
	log "bundleVersion = ${bundleVersion}"
	log "bundleVersionCode = ${bundleVersionCode}"
	
	if [[ ! -f "${UNITY_EXE_PATH}" ]]; then
		logError "Unity execution not exist: ${UNITY_EXE_PATH}"
	fi
	if [ ! -d "${CLIENT_PROJECT_PATH}" ]; then
		logError "client directory not exist: ${CLIENT_PROJECT_PATH}"
	fi
}

function git_process() {
	echo ""
	cd "${GIT_PROJECT_PATH}"
	
	git reset --hard HEAD
	[ $? -ne 0 ] && logError "git reset failed!"

	# remove untracked files and directories not include ignored files.
	git clean -fd
	[ $? -ne 0 ] && logError "git clean failed!"

	git pull -v
	[ $? -ne 0 ] && logError "git pull failed!"
}


function build() {
	echo ""
	local executeMethod="BuildAndroid.BuildAPK"
	if [[ "$buildType" == "2" ]]; then
		executeMethod="BuildAndroid.BuildAAB"
	elif [[ "$buildType" == "3" ]]; then
		executeMethod="BuildAndroid.BuildAndroidGradleProject"
	fi
	
	set -x # Activate debugging
	
	$UNITY_EXE_PATH -quit -batchmode \
		-buildTarget android \
		-projectPath "${CLIENT_PROJECT_PATH}" \
		-executeMethod "${executeMethod}" \
		-bundleVersion "${bundleVersion}" \
		-bundleVersionCode "${bundleVersionCode}" \
		-logFile "$WORK_DIR/build_android_log.txt"
	
	set +x # Deactivate debugging
}

function check_build_result() {
	echo ""
	local timestamp=$(date +%Y%m%d_%H%M%S)
	destFileName="<game-product-name>_${bundleVersion}_${bundleVersionCode}_${timestamp}"
	if [[ "$buildType" == "1" ]]; then
		log "start to check apk file..."
		local apkFilePath="${CLIENT_PROJECT_PATH}/<path-to.apk>"
		if [[ -f "${apkFilePath}" ]]; then
			log "buil apk success: ${apkFilePath}"
			destFileName="${destFileName}.apk"
			upload "${apkFilePath}" "${destFileName}"
		else
			logError "build apk failure! Not exist at: ${apkFilePath}"
		fi
	elif [[ "$buildType" == "2" ]]; then
		log "start to check aab file..."
		local aabFilePath="${CLIENT_PROJECT_PATH}/<path-to.aab>"
		if [[ -f "${aabFilePath}" ]]; then
			log "buil aab success: ${aabFilePath}"
			destFileName="${destFileName}.aab"
			upload "${aabFilePath}" "${destFileName}"
		else
			logError "build aab failure! Not exist at: ${aabFilePath}"
		fi
	elif [[ "$buildType" == "3" ]]; then
		log "start to check android gradle project..."
		# TODO
	fi
}

function upload() {
	echo ""
	log "start to upload..."
	local srcFilePath="$1"
	local destFileName="$2"
	local destFilePath="<remote-url>/$destFileName"

	rsync -avzl --progress "${srcFilePath}" "${destFilePath}"
	[ $? -ne 0 ] && logError "failed to upload: ${destFilePath}"

	log "upload success: ${destFilePath}"
}

function main() {
	check_parameter
	git_process
	build
	check_build_result
}


echo "\nThis script was invoked by execute:"
echo "sh $0 $@"
echo ""

# Iterate over all parameters
while (( "$#" )); do
  	case "$1" in
    	-buildType|--buildType)
	      	buildType="$2"
	      	shift 2
	      	;;
    	-bundleVersion|--bundleVersion)
      		bundleVersion="$2"
      		shift 2
      		;;
	    -bundleVersionCode|--bundleVersionCode)
		    bundleVersionCode="$2"
		    shift 2
		    ;;
    	--)
      		shift
      		break
      		;;
    	-*|--*=)
      		echo "Error: Unsupported flag: $1" >&2
      		shift
      		;;
    	*) # preserve positional arguments
      		echo "Error: Unsupported positional argument: $1" >&2
      		exit 1
      		;;
  	esac
done

main


