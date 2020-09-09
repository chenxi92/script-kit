#! /bin/bash


function safe_rm()
{
	# echo "parameter numbers: $#"

	move_to_trash()
	{
		if [[ -d "$1" ]]; then
			echo "Moving 📁 to Trash: $1"
			mv "$1" ~/.Trash/
			return
		elif [[ -e "$1" ]]; then
			echo "Moving 📄 to Trash: $1"
			mv "$1" ~/.Trash/
			return
		fi
		echo "Source not exist: $1"
	}

	for i in "$@"; do
		if [[ "$i" == "-r" ]]; then
			continue
		elif [[ "$i" == "-f" ]]; then
			continue
		elif [[ "$i" == "-rf" ]]; then
			continue
		elif [[ "$i" == "-fr" ]]; then
			continue
		fi

		move_to_trash "$i"
	done
}
safe_rm "$@"




