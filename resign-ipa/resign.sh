#! /bin/bash

# put the ipa file with the same folder as the script
# setup the `provisionFileName`
# sh resign.sh

set -eu

RED="\033[31m"
BLUE="\033[34m"
NC="\033[0m" # no color

provisionFileName="xxxx"

ipaPath=$(find $(pwd) -name *.ipa) || {
    echo "[$(pwd)] must only exist one ipa file, please checkout."
    exit 1
}
workDir=$(dirname $ipaPath)
ipaName=${ipaPath##*/} # delete the last '/' 
ipaName="${ipaName%.*}_resign.ipa" # delete last '.' and append '_resign.ipa'
outputIpaPath="${workDir}/${ipaName}"
entitlementPlistPath="${workDir}/entitlements.plist"

function clean() {
    if [[ -d "${workDir}/Payload" ]]; then
	rm -rf "${workDir}/Payload"
    fi
    if [[ -f "${entitlementPlistPath}" ]]; then
        rm -rf "${entitlementPlistPath}"
    fi
}

function log() {
    echo "$@"
}

function quit() {
    echo "$@"
    clean
    exit 1
}

log "working in: ${workDir}"
log "unzip: ${BLUE}${ipaPath}${NC}"
clean && unzip -q "${ipaPath}" || quit "unzip ipa fail"

appPath=$(find ${workDir} -name *.app -type d)
log "app file: ${BLUE}${appPath}${NC}"

# bussiness
# jsonPath=$(find ${appPath} -name parameter.json)
# if [[ -f $jsonPath ]]; then
#     log "modify $jsonPath"
#     sed -i ' ' "s/ios-pay.godsstrike.com/pre-test-aws-kmpay.karmasgame.com/g" $jsonPath
# fi

provisionFilePath=""
provisionFileDir="$HOME/Library/MobileDevice/Provisioning Profiles"
for file in $(ls -Ut "${provisionFileDir}"); do
    name=$(/usr/libexec/PlistBuddy -c 'Print :Name' /dev/stdin <<< $(/usr/bin/security cms -D -i "${provisionFileDir}/$file"))
    if [[ ${name} == "${provisionFileName}" ]]; then
	provisionFilePath="${provisionFileDir}/${file}"
	log "provision file path: $provisionFilePath"
	break
    fi
done
if [[ -z ${provisionFilePath} ]]; then
    quit "not exist provision file: ${provisionFileName}"
fi
embededProvisionFile="${appPath}/embedded.mobileprovision"
if [[ -f ${embededProvisionFile} ]]; then
    rm -rf ${embededProvisionFile}
fi
cp "${provisionFilePath}" "${embededProvisionFile}"

/usr/libexec/PlistBuddy -x -c 'Print :Entitlements' /dev/stdin <<< $(/usr/bin/security cms -D -i "${embededProvisionFile}") > $entitlementPlistPath

# bundleId
bundleId=$(/usr/libexec/PlistBuddy -c 'Print :Entitlements:application-identifier' /dev/stdin <<< $(/usr/bin/security cms -D -i "${embededProvisionFile}"))
bundleId=${bundleId#*.}
log "bundleId = $bundleId"
/usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier '${bundleId}'" "${appPath}/Info.plist"

# code sign info
codeSignValue=$(/usr/libexec/PlistBuddy -c 'Print :DeveloperCertificates:0' /dev/stdin <<< $(security cms -D -i "${embededProvisionFile}") \
        | openssl x509 -inform DER -noout -subject \
        | sed -n '/^subject/s/^.*CN=\(.*\)\/OU=.*/\1/p')
if [[ -z "${codeSignValue}" ]]; then
    log "not find codeSignValue, try to another way"
    codeSignValue=$(/usr/libexec/PlistBuddy -c 'Print :DeveloperCertificates:0' /dev/stdin <<< $(security cms -D -i "${embededProvisionFile}") \
        | openssl x509 -inform DER -noout -subject \
        | awk -F"CN = " '{print $2}' \
        | awk -F", " '{print $1}')
fi
log "codeSignValue = $codeSignValue"

resignPaths=$(find "$appPath" -d -name *.app \
							  -o -name *.framework \
							  -o -name *.dylib \
							  -o -name *.appex -type f \
							  -o -name *.so -type f \
							  -o -name *.o -type f \
							  -o -name *.vis -type f \
							  -o -name *.pvr -type f \
							  -o -name *.egg -type f \
							  -o -name *.0 -type f)
for path in $resignPaths; do
    log "[resign] ${RED}/usr/bin/codesign -vvv -fs \"${codeSignValue}\" --no-strict --entitlements \"${entitlementPlistPath}\" \"${path}\" ${NC}"
    /usr/bin/codesign -vvv -fs "${codeSignValue}" --no-strict --entitlements "${entitlementPlistPath}" "${path}"
    echo ""
done

echo ""
log "verify ipa ..."
/usr/bin/codesign --verify "${appPath}" || quit "verify ipa fail"
log "verify success"

echo ""
payloadPath=$(dirname "${appPath}")
if [[ -f "${outputIpaPath}" ]]; then
    rm -rf "${outputIpaPath}"
fi
log "zip: \n${BLUE}${payloadPath}${NC}\n->\n${BLUE}${outputIpaPath}${NC}"
(cd $(dirname "${payloadPath}") && zip -qyr "${outputIpaPath}" * && (cd "${workDir}")) || quit "zip ipa fail"
log "zip success"

echo ""
clean
log "clear success"

