#! /bin/bash

# Date: 2021-1-14
# Author: Peak
# Function: 该脚本用于从Unity工程中导出ipa文件

# ===== 打包证书参数配置 ===== #
# 描述文件名称, 每次更换描述文件需要改变名称
PROVISIONING_PROFILE_DEV="hsh_dev_2021_1_14"
PROVISIONING_PROFILE_DIS="hsh_dis_2021_1_8"

# ===== 全局参数 ===== #
TARGET="Unity-iPhone"                   # 默认值，不需要修改
START=$(date +%s)                       # 记录开始时间, 单位: 秒
EXECUTE_SUCCESS=0                       # 标记是否执行成功
ERROR_LOG_PATH="archive.log"            # 错误信息输出目录
CURRENT_TIME=$(date +%Y-%m-%d-%H-%M-%S) # 格式化当前时间

# ===== 路径配置 ===== #
UNITY_PATH="/Applications/Unity/Unity.app/Contents/MacOS/Unity"        # UNITY程序的路径
WORK_DIR="/Users/admin/m1"                                             # 工作空间
UNITY_PROJECT_PATH="${WORK_DIR}/heros_client"                          # Unity项目路径
XCODE_PROJECT_PATH="${WORK_DIR}/exportDir/iOS/Project_${CURRENT_TIME}" # 导出Xcode工程目录
OUTPUT_WORK_DIR="${WORK_DIR}/ipa_des"                                  # ipa文件输出目录
UNITY_LOG_PATH="$HOME/Library/Logs/Unity/Editor.log"                   # unity 

function infolog() {
    echo "$(date '+%Y-%m-%d %H:%M:%S')  [INFO] --- $*"
}
function errorlogExit() {
    echo "$(date '+%Y-%m-%d %H:%M:%S')  [ERROR] --- $*"
    exit 1
}
function calculateTime() {
    local end=$(date +%s)
    local runtime=$((end-START))
    infolog "执行时间: $((runtime/60)) 分 $((runtime%60)) 秒."
}
function egress()
{
    # 脚本执行成功，输出时间
    if [[ ${EXECUTE_SUCCESS} == 1 ]]; then
        calculateTime
        return
    fi
    if [[ -f ${ERROR_LOG_PATH} ]]; then
        infolog "begin extract error info from ${ERROR_LOG_PATH}"
        # 提取错误日志信息
        echo ""
        tail -n 30 ${ERROR_LOG_PATH}
        echo ""
    else
        echo ""
        echo ""
        tail -n 30 ${UNITY_LOG_PATH}
        echo ""
        echo ""
    fi
    # 输出执行时间
    calculateTime
}

# 捕获退出信号
trap egress EXIT

function extract_code_sign() 
{
    # get code sign from provision profile's name
    if [[ -z $1 ]]; then
        return
    fi
    function get_code_sign() 
    {
        local codeSignValue=$(/usr/libexec/PlistBuddy -c 'Print :DeveloperCertificates:0' \
        /dev/stdin <<< $(security cms -D -i "$1") \
        | openssl x509 -inform DER -noout -subject \
        | sed -n '/^subject/s/^.*CN=\(.*\)\/OU=.*/\1/p')
        echo ${codeSignValue}
    }
    local codeSign=""
    for file in $(ls "$HOME/Library/MobileDevice/Provisioning Profiles"); do
        path_to_mobileprovision="$HOME/Library/MobileDevice/Provisioning Profiles/$file"
        name=$(/usr/libexec/PlistBuddy -c 'Print :Name' /dev/stdin <<< $(security cms -D -i "${path_to_mobileprovision}"))
        if [[ $name == "$1" ]]; then
            codeSign=$(get_code_sign "${path_to_mobileprovision}")
            break
        fi
    done
    echo "${codeSign}"
}

function exportXcodeProjectFromUnity() {
    # 从Unity中导出Xcode工程
    infolog "begin export Xcode project from Unity ..."
    infolog "Xcode project path = ${XCODE_PROJECT_PATH}"

    if [[ ! -f ${UNITY_PATH} ]]; then
        errorlogExit "Unity 程序 [${UNITY_PATH}] 不存在"
    fi

    if [[ ! -d ${UNITY_PROJECT_PATH} ]]; then
        errorlogExit "Unity 工程 [${UNITY_PROJECT_PATH}] 不存在"
    fi

    if [[ -d ${XCODE_PROJECT_PATH} ]]; then
        infolog "删除已经存在的Xcode工程"
        rm -rf ${XCODE_PROJECT_PATH}
    fi

    infolog "execute command: ${UNITY_PATH} -quit -batchmode -projectPath ${UNITY_PROJECT_PATH} -executeMethod AutoBuilder.BuildForiOS version-$1 xcodeVersion-$2 cocdeVersion-$3 btype-$4 project-${XCODE_PROJECT_PATH}"
    $UNITY_PATH -quit -batchmode -projectPath $UNITY_PROJECT_PATH \
    -executeMethod AutoBuilder.BuildForiOS version-$1 xcodeVersion-$2 cocdeVersion-$3 btype-$4 project-${XCODE_PROJECT_PATH} 

    if [[ ! -d ${XCODE_PROJECT_PATH} ]]; then
        errorlogExit "Unity 导出失败， 请检查 ${UNITY_LOG_PATH} 文件查看错误信息"
    fi
    infolog "finish export Xcode project: ${XCODE_PROJECT_PATH}"
    echo ""
}

function exportArchive() {
    # 从Xcode 工程导出 archive 文件
    infolog "archive begin ..."

    if [[ ! -d ${XCODE_PROJECT_PATH} ]]; then
        errorlogExit "xcode 工程不存在: ${XCODE_PROJECT_PATH}"
    fi
    cd ${XCODE_PROJECT_PATH}

    if [[ $# != 2 ]]; then
        errorlogExit "第一个参数 xcarchive 文件路径; 第二个参数编译类型."
    fi
    
    local archiveFilePath=$1
    if [[ -d ${archiveFilePath} ]]; then
        rm -rf ${archiveFilePath}
    fi

    if [[ -d "${XCODE_PROJECT_PATH}/build" ]]; then
        rm -rf "${XCODE_PROJECT_PATH}/build"
    fi
    xcodebuild clean -quiet
    infolog "clean project success"

    local sign
    local profile
    local config
    if [[ $2 == "dev" ]]; then
        profile=${PROVISIONING_PROFILE_DEV}
        config=Debug
    elif [[ $2 == "dis" ]]; then
        profile=${PROVISIONING_PROFILE_DIS}
        config=Release
    else
        errorlogExit "编译类型错误, dev, dis 有效."
    fi
    sign=$(extract_code_sign "${profile}")
    if [[ -z ${sign} ]]; then
        # 判断签名信息是否不存在 或者 为空
        errorlogExit "从 ${profile} 中提取签名信息错误"
    fi

    infolog "archive with parameter config=${config}"
    infolog "archive with parameter TARGET=${TARGET}"
    infolog "archive with parameter sign=${sign}"
    infolog "archive with parameter profile=${profile}"
    infolog "archive with parameter archiveFilePath=${archiveFilePath}"
    
    infolog "execute command: xcodebuild -sdk iphoneos -configuration ${config} -target ${TARGET} -scheme ${TARGET} CODE_SIGN_IDENTITY=\"${sign}\" PROVISIONING_PROFILE_SPECIFIER=${profile} -archivePath \"${archiveFilePath}\" "
    xcodebuild \
        -sdk iphoneos \
        -configuration ${config} \
        -target ${TARGET} \
        -scheme ${TARGET} \
        CODE_SIGN_IDENTITY="${sign}" \
        PROVISIONING_PROFILE_SPECIFIER="${profile}" \
        -archivePath ${archiveFilePath} \
        archive > ${ERROR_LOG_PATH}

    if [[ ! -d ${archiveFilePath} ]]; then
        errorlogExit "Export xcarchive failed, please check detail info in: ${ERROR_LOG_PATH}"
    fi
    infolog "archive end"
    echo ""
}

function generateOptionPlist() {
    if [[ $# != 2 ]]; then
        errorlogExit "第一个参数传入编译类型; 第二个参数传入plist文件路径."
    fi
    cd ${OUTPUT_WORK_DIR}

    local buildType=$1
    local plistFilePath=$2
    if [[ -f ${plistFilePath} ]]; then
        rm -rf ${plistFilePath}
    fi

    local teamID=$(extract_code_sign "${PROVISIONING_PROFILE_DIS}" | sed 's/.*(\(.*\))/\1/g')
    infolog "extract teamID = ${teamID}"

    local bundleID
    for file in $(ls "$HOME/Library/MobileDevice/Provisioning Profiles"); do
        path_to_mobileprovision="$HOME/Library/MobileDevice/Provisioning Profiles/$file"
        name=$(/usr/libexec/PlistBuddy -c 'Print :Name' /dev/stdin <<< $(security cms -D -i "${path_to_mobileprovision}"))
        if [[ $name == ${PROVISIONING_PROFILE_DEV} ]]; then
            bundleID=$(/usr/libexec/PlistBuddy -c 'Print :Entitlements:application-identifier' /dev/stdin <<< $(security cms -D -i "${path_to_mobileprovision}") | cut -d '.' -f 2-)
            infolog "extract bundle id = $bundleID"
            bundleID=$(echo $bundleID | sed 's/\./\\./g')
            infolog "handled bundle id = $bundleID"
            break
        fi
    done
    if [[ -z ${bundleID} ]]; then
        errorlogExit "提取 bundleID 失败"
    fi

    echo '<?xml version="1.0" encoding="UTF-8"?><!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd"><plist version="1.0"><dict/></plist>' > ${plistFilePath}
    plutil -insert destination -string "export" ${plistFilePath}
    plutil -insert provisioningProfiles -xml "<dict/>" ${plistFilePath}
    plutil -insert signingStyle -string "manual" ${plistFilePath}
    plutil -insert stripSwiftSymbols -bool true ${plistFilePath}
    plutil -insert teamID -string "${teamID}" ${plistFilePath}
    if [[ ${buildType} == 'dev' ]]; then
        plutil -insert compileBitcode -bool false ${plistFilePath}
        plutil -insert method -string "development" ${plistFilePath}
        plutil -insert provisioningProfiles.${bundleID} -string ${PROVISIONING_PROFILE_DEV} ${plistFilePath}
        plutil -insert signingCertificate -string "iPhone Developer" ${plistFilePath}
        plutil -insert thinning -string "<none>" ${plistFilePath}
    elif [[ ${buildType} == 'dis' ]]; then
        plutil -insert method -string "app-store" ${plistFilePath}
        plutil -insert provisioningProfiles.${bundleID} -string ${PROVISIONING_PROFILE_DIS} ${plistFilePath}
        plutil -insert signingCertificate -string "Apple Distribution" ${plistFilePath}
        plutil -insert uploadSymbols -bool false ${plistFilePath}
    else
        errorlogExit "生成option.plist文件错误，类型识别错误"
    fi
}

function archiveExportIPA() {
    # archive 文件导出 ipa
    infolog "export to ipa begin ... "

    if [[ $# != 3 ]]; then
        errorlogExit "第一个参数 ipa 文件路径; 第二个参数 archive 文件路径; 第三个参数编译类型."
    fi

    local ipaFilePath=$1
    local archivePath=$2
    local buildType=$3
    infolog "export ipa with parameter ipaFilePath = ${ipaFilePath}"
    infolog "export ipa with parameter archivePath = ${archivePath}"
    infolog "export ipa with parameter buildType = ${buildType}"

    local exportOptionsPlist=${OUTPUT_WORK_DIR}/option_${buildType}.plist
    generateOptionPlist ${buildType} ${exportOptionsPlist}
    infolog "generate option plist success."

    local exportPath=${OUTPUT_WORK_DIR}/${buildType}
    infolog "execute command: xcodebuild -exportArchive -archivePath ${archivePath} -exportOptionsPlist ${exportOptionsPlist} -exportPath ${exportPath} -quiet"
    xcodebuild \
        -exportArchive \
        -archivePath ${archivePath} \
        -exportOptionsPlist ${exportOptionsPlist} \
        -exportPath ${exportPath} \
        -quiet >> ${ERROR_LOG_PATH}

    local exportIpaFile
    for file_name in `(ls ${exportPath})`; do
        local file_suffix=${file_name##*.}
        if [[ ${file_suffix} == "ipa" ]]; then
            exportIpaFile=${exportPath}/${file_name}
            break;
        fi
    done
    if [[ ! -f ${exportIpaFile} ]]; then
        errorlogExit "导出ipa文件失败."
    fi
    infolog "moving ${exportIpaFile} to ${ipaFilePath}"
    mv ${exportIpaFile} ${ipaFilePath}
}

function buidIPAWithType()
{
    local buildType=$1
    local buildVersion=$2
    local bType=$3
    local environment=""
    if [[ $bType == "1" ]]; then
        environment=inner
    elif [[ $bType == "2" ]]; then
        environment=outer
    elif [[ $bType == "3" ]]; then
        environment=outerTest
    elif [[ $bType == "4" ]]; then
        environment=innerInput
    elif [[ $bType == "5" ]]; then
        environment=outerInput
    fi

    appName=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleDisplayName' "${XCODE_PROJECT_PATH}/Info.plist" | sed 's/ /_/g')
    local archiveFilePath=${OUTPUT_WORK_DIR}/${appName}_${buildType}.xcarchive
    # 2. 生成 archive 文件
    infolog "begin archive at: ${archiveFilePath}"
    exportArchive ${archiveFilePath} ${buildType}

    # ipa 文件路径
    local ipaFileDir=${OUTPUT_WORK_DIR}/${buildType}
    if [[ -d ${ipaFileDir} ]]; then
        rm -rf ${ipaFileDir}
    fi
    mkdir ${ipaFileDir}

    # ipa 文件名称
    local ipaFileName="${appName}_${environment}_${buildVersion}_${CURRENT_TIME}_${buildType}.ipa"
    infolog "ipa 名称 = ${ipaFileName}"
    ipaFilePath=${ipaFileName}

    # 3. archive 导出 ipa 文件
    archiveExportIPA ${ipaFilePath} ${archiveFilePath} ${buildType}
}

function run() 
{
    # 1. 导出 Xcode 工程
    exportXcodeProjectFromUnity $1 $2 $3 $4

    if [[ $ftype == "1" ]]; then
        infolog "开始导出 dev 包 ..."
        buidIPAWithType dev $1 $4
    elif [[ $ftype == "2" ]]; then
        infolog "开始导出 dis 包 ..."
        buidIPAWithType dis $1 $4
    elif [[ $ftype == "3" ]]; then
        infolog "开始导出 dev 包 ..."
        buidIPAWithType dev $1 $4

        echo ""
        echo "----------------------------"
        echo "----------------------------"
        echo ""

        infolog "开始导出 dis 包 ..."
        buidIPAWithType dis $1 $4
    fi

    EXECUTE_SUCCESS=1
}

infolog "location: $(pwd)"
ip=$(ifconfig en0 | grep "inet 1" | awk '{print $2}')
infolog "current ip: ${ip}"
for i in "$@"; do
    infolog "Run script with parameters: $i"
done

passward="123456"
infolog "执行解锁钥匙串命令: security unlock-keychain -p ${passward} $HOME/Library/Keychains/login.keychain"
security unlock-keychain -p ${passward} $HOME/Library/Keychains/login.keychain


version=$1
xcodeVersion=$2
codeVersion=$3
btype=$4
ftype=$5

run $version $xcodeVersion $codeVersion $btype $ftype


