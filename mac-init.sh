#! /bin/bash

set -u

echo "========================================================= "
echo "                      Mac 环境部署脚本                      "
echo "author: 陈希"
echo "date: 2021-5-20"
echo "========================================================= "


red="\033[31m"
blue="\033[34m"
reset="\033[0m"
dateFormate=$(date "+%Y-%m-%d %H:%M:%S")

abort() {
	printf "%s\n" "$@"
	exit 1
}

log() {
	printf "[${blue}${dateFormate}${reset}] %s\n" "$@"
}

warning() {
	printf "[${blue}${dateFormate}${reset}] ${red}Warning${reset}: %s\n" "$@"
}

# source: https://github.com/Homebrew/install
installHomebrew() {
	brew --version >/dev/null
	if [[ $? -ne 0 ]]; then
		log "开始安装 Homebrew ... "
		
		/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
		
		echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> $HOME/.zprofile
		eval "$(/opt/homebrew/bin/brew shellenv)"
		log "完成安装 Homebrew "
	else
		log "Homebrew 已经安装!"
	fi
}
uninstallHomevrew() {
	warning "开始卸载 Homebrew ..."
	/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/uninstall.sh)"
	warning "卸载安装 Homebrew "
}

installOhMyZsh() {
	ls -la ~/ | grep .oh-my-zsh >/dev/null
	if [[ $? -ne 0 ]]; then
		log "开始安装 oh-my-zsh ... "
		sh -c "$(curl -fsSL https://raw.github.com/robbyrussell/oh-my-zsh/master/tools/install.sh)"
		log "完成安装 oh-my-zsh "
		modifyOhMyZsh
	else
		log "oh-my-zsh 已经安装!"
	fi
}
modifyOhMyZsh() {
	log "开始配置 oh-my-zsh ..."
	if [[ ! -f ~/.zshrc ]]; then
		warning ".zshrc not exist"
		return
	fi
	log "修改 theme ..."
	sed -i '' 's/ZSH_THEME="robbyrussell"/ZSH_THEME="random"/g' ~/.zshrc

	log "设置常用插件 ..."
	sed -i '' 's/plugins=(.*)/plugins=(git zsh-syntax-highlighting zsh-autosuggestions zsh-completions)/g' ~/.zshrc
	
	log "安装 zsh-syntax-highlighting 插件"
    git clone git://github.com/zsh-users/zsh-syntax-highlighting    ${ZSH_CUSTOM:=~/.oh-my-zsh/custom}/plugins/zsh-syntax-highlighting >/dev/null 2>&1
    
    log "安装 zsh-autosuggestions 插件"
	git clone https://github.com/zsh-users/zsh-autosuggestions      ${ZSH_CUSTOM:-~/.oh-my-zsh/custom}/plugins/zsh-autosuggestions >/dev/null 2>&1
    
    log "安装 zsh-completions 插件"
    git clone https://github.com/zsh-users/zsh-completions          ${ZSH_CUSTOM:=~/.oh-my-zsh/custom}/plugins/zsh-completions >/dev/null 2>&1

    source ~/.zshrc
}

systemCommand(){
	cmdLines=(
		"ag"
		"jq"
		"telnet"
		"git-lfs"
		"wget"
	)
	for cmd in "${cmdLines[@]}"; do
		soft=$(which $cmd)
		if [ "$soft" ] >/dev/null 2>&1; then
            log "[${cmd}] 已安装!"
        else
            if [[ ${cmd} == "ag" ]]; then
            	cmd="the_silver_searcher"
            fi
            log "[${cmd}] 安装中......"
            brew install ${cmd}

            if [[ ${cmd} == "git-lfs" ]]; then
            	# 解决 jenkins 打包时, 下载大文件失败
            	cp $(which git-lfs) /Applications/Xcode.app/Contents/Developer/usr/libexec/git-core
            fi
        fi
	done
}

installJenkins() {
	# source: https://www.jenkins.io/download/weekly/macos/
	jenkins --version >/dev/null
	if [[ $? -ne 0 ]]; then
		log "开始安装 jenkins ... "
		log "begin install the latest Weekly version: jenkins"
		brew install jenkins

		# 解决通过 ip 访问本机jenkins
		log "modify homebrew.mxcl.jenkins.plist file ..."
		
		plistDir="/usr/local/Cellar/jenkins"
		UNAME_MACHINE="$(/usr/bin/uname -m)"
		if [[ "$UNAME_MACHINE" == "arm64" ]]; then
			# On ARM Mac
			plistDir="/opt/homebrew/Cellar/jenkins"
		fi
		plist=$(find ${plistDir} -name "homebrew.mxcl.jenkins.plist")
		sed -i '' "s/127.0.0.1/0.0.0.0/g" "${plist}"
	else
		log "jenkins 已经安装!"
		log "jenkins 常用命令..."
		echo "启动 Jenkins: brew services start jenkins"
		echo "重启 Jenkins: brew services restart jenkins"
		echo "更新 Jenkins: brew upgrade jenkins"
	fi
}

installPython3() {
	python3 --version >/dev/null
	if [[ $? -ne 0 ]]; then
		log "开始安装 python3 ... "
		brew install python3
	else
		log "python3 已经安装!"
	fi
}

main() {
	echo ""
	echo "install brew"
	# uninstallHomevrew
	installHomebrew

	echo ""
	echo "install oh-my-zsh"
	installOhMyZsh

	echo ""
	echo "install commands"
	systemCommand

	echo ""
	echo "install jenkins"
	installJenkins

	echo ""
	echo "install python3"
	installPython3
}


main
