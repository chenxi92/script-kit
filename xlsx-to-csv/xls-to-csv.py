# coding:utf-8
import xlrd
import csv
import os
import sys

# 处理中文字符
reload(sys)
sys.setdefaultencoding('utf8')


def xlsx_to_csv(xlsx_path, csv_path, delimiter=","):
    """
    xlsx_path xlsx 文件绝对路径
    csv_path  csv 文件绝对路径
    delimiter 分隔符，默认使用","分割
    """
    with open(csv_path, "w") as f:
        wb = xlrd.open_workbook(xlsx_path)
        wr = csv.writer(f, delimiter=delimiter)
        for index in range(wb.nsheets):
            sh = wb.sheet_by_index(index)
            # print("\n名称={0}\n行={1}\n列={2}".format(sh.name, sh.nrows, sh.ncols))
            for nrows in range(sh.nrows):
                row_values = []
                for ncols in range(sh.ncols):
                    cell_value = sh.cell(nrows, ncols).value
                    if cell_value == '':
                        cell_value = '__'
                    elif isinstance(cell_value, unicode):
                        cell_value = cell_value.encode('utf-8')
                    elif isinstance(cell_value, float) or isinstance(cell_value, int):
                        cell_value = str(int(cell_value)).encode('utf-8')
                    row_values.append(cell_value)
                wr.writerow(row_values)


def main():
    current_dir = os.getcwd()
    for file in os.listdir(current_dir):
        if os.path.isdir(file) or file.startswith("."):
            continue
        [file_name, file_suffix] = file.split(".")
        if file_suffix != "xlsx":
            continue
        xlsx_file_path = os.path.join(current_dir, file)
        csv_file_path = os.path.join(current_dir, file_name + ".csv")
        xlsx_to_csv(xlsx_file_path, csv_file_path, delimiter="|")


if __name__ == '__main__':
    main()
