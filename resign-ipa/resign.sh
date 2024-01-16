#!/bin/bash
# 
# Resign ipa file with specific provision file.
# 
# Find the ipa file at the current directory or 
# you can specify the location as the first parameter
#
# Usage:
# sh resign.sh <ipa-file-path>
# sh resign.sh

set -eu
# set -x # debug mode

RED="\033[31m"
BLUE="\033[34m"
NC="\033[0m" # no color
PROVISION_FILE_INSTALL_DIR="$HOME/Library/MobileDevice/Provisioning Profiles"
PROVISION_FILE_NAME="xxxx"

echo "resign ipa with provision file: ${PROVISION_FILE_NAME}"

ipa_file_path=${1:-$(find $(pwd) -name *.ipa)}
if [[ ! -f "${ipa_file_path}" ]]; then
    echo "Not specify ipa file or not find ipa file at current directory! [${ipa_file_path}]"
    exit 1
fi

work_dir=$(dirname $ipa_file_path)
ipa_name=${ipa_file_path##*/} # delete the last '/' 
ipa_name="${ipa_name%.*}_resigned.ipa" # delete last '.' and append '_resign.ipa'
output_ipa_file_path="${work_dir}/${ipa_name}"
entitlement_plist_path="${work_dir}/entitlements.plist"


function clean() {
    if [[ -d "${work_dir}/Payload" ]]; then
	   rm -rf "${work_dir}/Payload"
    fi
    if [[ -f "${entitlement_plist_path}" ]]; then
        rm -rf "${entitlement_plist_path}"
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

log "working in: [${work_dir}]"
log "unzip: [${ipa_file_path}]"
clean && unzip -q "${ipa_file_path}" || quit "unzip ipa fail"

app_path=$(find ${work_dir} -name *.app -type d)
log "app file: ${BLUE}${app_path}${NC}"

# bussiness
# parameter_file_path=$(find ${app_path} -name "parameter.json")
# if [[ -f $parameter_file_path ]]; then
#     log "modify [$parameter_file_path]"
#     sed -i '' "s/aaa/bbb/g" "$parameter_file_path"
# fi

provision_file_path=""
for file in $(ls -Ut "${PROVISION_FILE_INSTALL_DIR}"); do
    name=$(/usr/libexec/PlistBuddy -c 'Print :Name' /dev/stdin <<< $(/usr/bin/security cms -D -i "${PROVISION_FILE_INSTALL_DIR}/$file"))
    if [[ ${name} == "${PROVISION_FILE_NAME}" ]]; then
	   provision_file_path="${PROVISION_FILE_INSTALL_DIR}/${file}"
	   log "provision file path: $provision_file_path"
	   break
    fi
done
if [[ -z ${provision_file_path} ]]; then
    quit "not exist provision file: ${PROVISION_FILE_NAME}"
fi
embededProvisionFile="${app_path}/embedded.mobileprovision"
if [[ -f ${embededProvisionFile} ]]; then
    rm -rf ${embededProvisionFile}
fi

log "copy provision file: \n${provision_file_path} \n->\n${embededProvisionFile}\n"
cp "${provision_file_path}" "${embededProvisionFile}"

/usr/libexec/PlistBuddy -x -c 'Print :Entitlements' /dev/stdin <<< $(/usr/bin/security cms -D -i "${embededProvisionFile}") > $entitlement_plist_path

# bundleId
bundleId=$(/usr/libexec/PlistBuddy -c 'Print :Entitlements:application-identifier' /dev/stdin <<< $(/usr/bin/security cms -D -i "${embededProvisionFile}"))
bundleId=${bundleId#*.}
log "bundleId = $bundleId"
/usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier '${bundleId}'" "${app_path}/Info.plist"


# code sign info
code_sign=$(/usr/libexec/PlistBuddy -c 'Print :DeveloperCertificates:0' /dev/stdin <<< $(security cms -D -i "${embededProvisionFile}") \
        | openssl x509 -inform DER -noout -subject \
        | sed -n '/^subject/s/^.*CN=\(.*\)\/OU=.*/\1/p')
if [[ -z "${code_sign}" ]]; then
    log "not find code_sign, try to another way"
    code_sign=$(/usr/libexec/PlistBuddy -c 'Print :DeveloperCertificates:0' /dev/stdin <<< $(security cms -D -i "${embededProvisionFile}") \
        | openssl x509 -inform DER -noout -subject \
        | awk -F"CN=" '{print $2}' \
        | awk -F", " '{print $1}')
fi
log "code_sign = $code_sign"


resign_file_array=$(find "$app_path" \
    -d -name *.app \
	-o -name *.framework \
	-o -name *.dylib \
	-o -name *.appex -type f \
	-o -name *.so -type f \
	-o -name *.o -type f \
	-o -name *.vis -type f \
	-o -name *.pvr -type f \
	-o -name *.egg -type f \
	-o -name *.0 -type f)

for resign_file in $resign_file_array; do
    log "[resign] ${RED}/usr/bin/codesign -vvv -fs \"${code_sign}\" --no-strict --entitlements \"${entitlement_plist_path}\" \"${resign_file}\" ${NC}"
    /usr/bin/codesign -vvv \
    -fs "${code_sign}" \
    --no-strict \
    --entitlements "${entitlement_plist_path}" "${resign_file}"
    echo ""
done

echo ""
log "verify ipa ..."
/usr/bin/codesign --verify "${app_path}" || quit "verify ipa fail"
log "verify success"

echo ""
payload_dir=$(dirname "${app_path}")
if [[ -f "${output_ipa_file_path}" ]]; then
    rm -rf "${output_ipa_file_path}"
fi
log "zip: \n${BLUE}${payload_dir}${NC}\n->\n${BLUE}${output_ipa_file_path}${NC}"
(cd $(dirname "${payload_dir}") && zip -q -r "${output_ipa_file_path}" "Payload" && (cd "${work_dir}")) || quit "zip ipa fail"
log "zip success"

echo ""
clean
log "clear success"

