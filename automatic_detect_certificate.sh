#! /bin/bash

set -eu

targetBundle="com.xxx.xxx"

CODE_SIGN_DEV=""
CODE_SIGN_DIS=""
PROVISIONING_PROFILE_DEV=""
PROVISIONING_PROFILE_DIS=""

function automatic_detect_certificate() {
	if [[ $# -lt 1 ]]; then
		echo "must specify buildId as the first parameter"
		exit 1
	fi
	targetBundle=$1
	echo "start looking provision file for buildId: [${targetBundle}]"

	local provisionFileDir="$HOME/Library/MobileDevice/Provisioning Profiles"

	# Use time when file was created for sorting or printing. 
	for file in $(ls -Ut "${provisionFileDir}"); do
		filePath="${provisionFileDir}/$file"
	    
	    selectBundleId=$(/usr/libexec/PlistBuddy -c 'Print :Entitlements:application-identifier' /dev/stdin <<< $(/usr/bin/security cms -D -i "${filePath}"))
		selectBundleId=${selectBundleId#*.}
		if [[ $selectBundleId == $targetBundle ]]; then
			echo ""

			name=$(/usr/libexec/PlistBuddy -c 'Print :Name' /dev/stdin <<< $(/usr/bin/security cms -D -i "${filePath}"))
			echo "Provision file name: $name"

			codeSignValue=$(/usr/libexec/PlistBuddy -c 'Print :DeveloperCertificates:0' /dev/stdin <<< $(security cms -D -i "${filePath}") \
	        | openssl x509 -inform DER -noout -subject \
	        | sed -n '/^subject/s/^.*CN=\(.*\)\/OU=.*/\1/p')
			
			if [[ $codeSignValue != "${codeSignValue#iPhone Distribution}" ]]; then
				echo "Distribution Codesign: $codeSignValue"
				if [[ $CODE_SIGN_DIS == "" ]]; then
					CODE_SIGN_DIS=$codeSignValue
					PROVISIONING_PROFILE_DIS=$name
				else
					echo "already select distribution code sign"
				fi
			else
				echo "Developer Codesign: $codeSignValue"
				if [[ $CODE_SIGN_DEV == "" ]]; then
					CODE_SIGN_DEV=$codeSignValue
					PROVISIONING_PROFILE_DEV=$name
				else
					echo "already select developer code sign"
				fi
			fi
		fi
	done
}


automatic_detect_certificate $targetBundle

echo ""
echo "--- finished detect certificate ---"
echo "Developer codeSign: $CODE_SIGN_DEV"
echo "Developer  profile: $PROVISIONING_PROFILE_DEV"

echo "Distribution codeSign: $CODE_SIGN_DIS"
echo "Distribution  profile: $PROVISIONING_PROFILE_DIS"



