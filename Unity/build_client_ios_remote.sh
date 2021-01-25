#!/bin/sh -il

set -eu

# ======== 路径配置 ========
workDir="/Users/gaojun/mp1/ios"
ipaDirPath="${workDir}/ipa_des"
outputSymbolPath="${workDir}/ipa_symbol"
sourcePath="${workDir}/source"
clientPath="${workDir}/heros_client"
packageShellPath="${sourcePath}/export-ios.sh"
# ======== 路径配置 ========

# ======== Bugly 相关参数 Begin ========
jarPath="/Users/gaojun/bin/buglySymboliOS.jar"
buglyAppId="f97b734f42"
buglyAppKey="4506371b-6946-4094-8f0c-8ec7315a4607"
# ======== Bugly 相关参数 End ========

SDKBundleName="ROTGame.bundle"
bundleId="com.zhiifan.ysxj"


function log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S')  [INFO] --- $*"
}

function detectDiskspaceUsage() {
    # 检查磁盘空间使用情况
    # 默认检查阈值是95%
    # 超过阈值先删除Xcode缓存, 再删除Xcode 工程备份文件

    # 确定阈值
    threshold=95

    function deleteDir() {
        if [[ -z $1 ]]; then
            return
        fi
        if [[ ! -d $1 ]]; then
            return
        fi
        size=$(du -sh $1 | awk '{print $1}')
        echo "begin delete ${size} file from: $1"
        rm -rf "$1"
    }

    function checkOverThreshold() {
        echo ""
        capacity=$(df -hl /System/Volumes/Data/ | grep "Data" | awk '{print $5}' | cut -d "%" -f1)
        echo "当前磁盘空间已经使用: ${capacity}%"
        if [[ ${capacity} -gt ${threshold} ]]; then
            return 1
        fi
        return 0
    }
    
    checkOverThreshold
    if [[ $? == 1 ]]; then
        # 检查Xcode缓存目录
        xcodeDerivedDataPath="${HOME}/Library/Developer/Xcode/DerivedData"
        deleteDir ${xcodeDerivedDataPath}
        
        # 检查Xcode工程备份目录
        for file in ${workDir}/exportDir/iOS/* ; do
            checkOverThreshold
            if [[ $? == 0 ]]; then
                return
            fi
            deleteDir "${workDir}/exportDir/iOS/$file"
        done
    fi
}

# 打出新的ipa包
function build()
{
    branchName="dev"
    if [ ! -z "$1" ]; then
		branchName=$1
	fi

    gameVersion=""
    if [[ ! -z "$2" ]]; then
		gameVersion=$2
	fi

    xcodeVersion=""
    if [[ ! -z "$3" ]]; then
        xcodeVersion=$3
    fi

    codeVer=""
    if [[ ! -z "$4" ]]; then
		codeVer=$4
	fi

    btype="1"
    if [[ ! -z "$5" ]]; then
		btype=$5
	fi

    ftype="1"
    if [[ ! -z "$6" ]]; then
        ftype=$6
    fi

    stype="0"
    if [[ ! -z "$7" ]]; then
        stype=$7
    fi

    certicateType="Test"
    if [[ ! -z "$8" ]]; then
        certicateType=$8
    fi 

    log "参数 branchName    = ${branchName}"
    log "参数 gameVersion   = ${gameVersion}"
    log "参数 xcodeVersion  = ${xcodeVersion}"
    log "参数 codeVer       = ${codeVer}"
    log "参数 btype         = ${btype}"
    log "参数 ftype         = ${ftype}"
    log "参数 stype         = ${stype}"
    log "参数 certicateType = ${certicateType}"

    log "清理 ${ipaDirPath} 目录下ipa文件 ..."
    rm -rf $ipaDirPath/*

    log "更新git ..."
    cd ${clientPath}
    git clean -df
    git checkout .

    # 判断分支是否存在
    git fetch
    branchExist=$(git branch --remote | grep ${branchName})
    if [ ! -n "$branchExist" ]; then
       log "目标分支 ${branchName} 不存在"
       exit -1
    fi
    git checkout ${branchName}
    git pull
    sleep 5

    log "覆盖 ProjectSettings.asset ..."
    cp -f "${sourcePath}/ProjectSettings.asset" "${clientPath}/ProjectSettings/ProjectSettings.asset"

    log "拷贝 SDK 配置文件"
    local jsonName="parameter.json"
    local sdkConfigSourcePath=""
    local sdkConfigTargetPath="${clientPath}/Assets/Plugins/IOS/res/${SDKBundleName}"
    if [[ $btype == "1" ]]; then
        log "SDK 内网环境"
        sdkConfigSourcePath="${sourcePath}/inner-product/${jsonName}"
    elif [[ $btype == "2" ]]; then
        log "SDK 正式环境"
        sdkConfigSourcePath="${sourcePath}/product/${jsonName}"
    elif [[ $btype == "3" ]]; then
        log "SDK 外网环境"
        sdkConfigSourcePath="${sourcePath}/product/${jsonName}"
    fi
    if [[ $stype == "1" ]]; then
        log "不支持模拟玩家登录"
        exit 1
    fi
    if [[ ! -f ${sdkConfigSourcePath} ]]; then
        log "${sdkConfigSourcePath} 不存在"
        exit 1
    fi
    cp ${sdkConfigSourcePath} ${sdkConfigTargetPath}

    log "检测磁盘空间 ..."
    detectDiskspaceUsage

    log "[execute command] /bin/sh ${packageShellPath} ${gameVersion} ${xcodeVersion} ${codeVer} ${btype} ${ftype} ${certicateType}"
    /bin/sh ${packageShellPath} ${gameVersion} ${xcodeVersion} ${codeVer} ${btype} ${ftype} ${certicateType}

    ipaFiles=$(find ${ipaDirPath} -type f -name "*.ipa")
    if [[ -z ${ipaFiles} ]]; then
        log "ipa不存在, 打包失败"
        exit 1
    fi
   
    log "上传ipa到内网"
    for file in `ls -f ${ipaDirPath}`;
    do 
        if [ "${file##*.}" = "ipa" ];then
            echo "上传ipa文件 = ${file}"
            md5 ${ipaDirPath}/${file}
            scp -P 22 -i ~/.ssh/id_rsa_jerrygao -r ${ipaDirPath}/${file} root@192.168.1.34:/data/html/client/ipa_bak/
        fi
    done 

    log "上传到内网完成服务器, 生成下载网页..."
    ssh -p22 root@192.168.1.34 -i ~/.ssh/id_rsa_jerrygao 'cd /data/publish/client/package && sh build_client_ios_download_mp1.sh ${bundleId}'
    
    log "下载包内网链接地址: http://192.168.1.34:8099/client/ipa-list.html"
    log "下载包外网链接地址: http://223.72.165.26:8099/client/ipa-list.html"
    
    cd $sourcePath

    if [[ $ftype == "3" ]]; then
        upload_symbol "${ipaDirPath}" "${gameVersion}" "$xcodeVersion"
    fi
}

function upload_symbol() 
{   
    # 参数列表： xcarchive 文件目录， 游戏版本号， Xcode工程版本号 
    echo ""
    echo "--------- 上传符号表 --------- "
    if [[ -z $1 ]]; then
        echo "xcarchive 目录, 不能为空"
        return
    fi
    if [[ -z $2 ]]; then
        echo "游戏版本号, 不能为空"
        return
    fi
    if [[ -z $3 ]]; then
        echo "Xcode工程版本号, 不能为空"
        return
    fi

    local gameVersion="$2"
    local xcodeVersion="$3"
    local archiveFilePath=$(find $1 -name "*_dis.xcarchive")
    if [[ ! -d ${archiveFilePath} ]]; then
        echo "$1 目录下不存在 xcarchive 文件"
        return
    fi

    local dsymFileSourcePath=$(find ${archiveFilePath} -type d -name "*.app.dSYM")
    if [[ ! -d ${dsymFileSourcePath} ]]; then
        echo ".app.dSYM 文件不存在: ${archiveFilePath}" 
        return
    fi

    echo "dsymFileSourcePath = ${dsymFileSourcePath}"
    local bundleId=$(/usr/libexec/PlistBuddy -c 'Print :ApplicationProperties:CFBundleIdentifier' ${archiveFilePath}/Info.plist)
    echo "bundleId = $bundleId"
    echo "gameVersion = ${gameVersion}"
    echo "xcodeVersion = ${xcodeVersion}"
    
    local uuid=$(xcrun dwarfdump --arch=arm64  --uuid ${dsymFileSourcePath} | awk -F ' ' '{print $2}')
    local targetFileName="${gameVersion}_${uuid}"
    local dsymFileTargetPath="${OutputSymbolPath}/${targetFileName}.app.dSYM"
    if [[ -d ${dsymFileTargetPath} ]]; then
        echo "Delete exist dSYM file in: ${dsymFileTargetPath}"
        rm -rf ${dsymFileTargetPath}
    fi

    echo ""
    echo "copy dsym file source: ${dsymFileSourcePath}"
    echo "copy dsym file target: ${dsymFileTargetPath}"
    cp -r ${dsymFileSourcePath} ${dsymFileTargetPath}
    echo ""

    local symbolFilePath="${OutputSymbolPath}/${targetFileName}.zip"
    echo "symbol file path = ${symbolFilePath}"
  
    function uploadFromJava()
    {
        echo ""
        echo "开始上传符号表 ..."
        local jarPath="$HOME/bin/buglySymboliOS.jar"
        if [[ ! -f ${jarPath} ]]; then
            echo "[$HOME/bin] 目录下不存在 buglySymboliOS.jar"
            echo "下载路径: https://bugly.qq.com/docs/user-guide/symbol-configuration-ios/?v=20200622202242#_5"
            return
        fi
        
        echo ""
        echo "[execute command] java -jar ${jarPath} -u -i ${dsymFileTargetPath} -id ${buglyAppId} 
        -key ${buglyAppKey} 
        -package ${bundleId} 
        -version ${xcodeVersion} 
        -o ${symbolFilePath}"

        java -jar ${jarPath} -u \
        -i ${dsymFileTargetPath} \
        -id ${buglyAppId} \
        -key ${buglyAppKey} \
        -package ${bundleId} \
        -version ${xcodeVersion} \
        -o ${symbolFilePath}

        echo ""
        echo "上传符号表结束"
        echo ""
    }
    
    function uploadFromHTTP()
    {
        echo ""
        echo "begin upload from curl ..."
        if [[ -f ${symbolFilePath} ]]; then
            echo "delete exist zip file: ${symbolFilePath}"
            rm -rf ${symbolFilePath}
        fi
        
        echo "[execute command] zip -r -q ${symbolFilePath} ${dsymFileTargetPath}"
        zip -r -q ${symbolFilePath} ${dsymFileTargetPath}

        local url="https://api.bugly.qq.com/openapi/file/upload/symbol?app_key=${buglyAppKey}&app_id=${buglyAppId}"
        echo "[execute command] curl -k ${url} 
        --form api_version=1 
        --form app_id=${buglyAppId} 
        --form app_key=${buglyAppKey} 
        --form symbolType=2 
        --form bundleId=${bundleId} 
        --form productVersion=${xcodeVersion} 
        --form fileName=${targetFileName}.zip 
        --form file=@${symbolFilePath} 
        --verbose"
        
        curl -k "${url}" \
        --form "api_version=1" \
        --form "app_id=${buglyAppId}" \
        --form "app_key=${buglyAppKey}" \
        --form "symbolType=2"  \
        --form "bundleId=${bundleId}" \
        --form "productVersion=${xcodeVersion}" \
        --form "fileName=${targetFileName}.zip" \
        --form "file=@${symbolFilePath}" \
        --verbose

        echo "finished upload from curl."
        echo ""
    }

    uploadFromHTTP
}

function showHelp()
{
    echo "Usage:";
    echo "$0 -h|--help               show help info";
    echo;
    echo "$0 -b|--build <branchName> <version> <codeVer> <btype>";
}

if [ -z $1 ];then
    showHelp;
    exit 1;
fi
case "$1" in
    -h|--help|?)
		showHelp;
        # exit 0;
        ;;
    -b|--build)
        build $2 $3 $4 $5 $6 $7 $8 $9;
        # exit 0;
		;;
    *)
		showHelp;
        # exit 0;
esac



# 测试代码
# 测试上传ipa到内网环境
function test_upload_ipa() 
{
    ipaDirPath="$1"
    scp -P 22 -i ~/.ssh/id_rsa_jerrygao -r $ipaDirPath root@192.168.1.34:/data/html/client/ipa_bak/

    echo ""
    echo ""
    echo "------------------------------打包完成并已经上传到内网服务器，准备生成对应html网页------------------------------"
    ssh -p22 root@192.168.1.34 -i ~/.ssh/id_rsa_jerrygao 'cd /data/publish/client/package && sh build_client_ios_download.sh'
    
    
    # git checkout .
    echo ""
    echo ""
    echo "------------------------------任务完成------------------------------"
    echo "------------------------------下载包链接地址: http://192.168.1.34:8099/client/ipa-list.html ------------------------------"
    echo "------------------------------下载包外网链接地址: http://223.72.165.26:8099/client/ipa-list.html ------------------------------"
}

# test_upload_ipa "/Users/gaojun/mp1/ios/ipa_des/ysxj_outer_2.2.3.21_2020-12-04-test_dev.ipa"