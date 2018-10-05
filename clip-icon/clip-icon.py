# coding:utf-8
import os
from PIL import Image

# first set icon name, the name usually in `Images.xcassets/AppIcon.appiconset/Contents.josn` file
# you may change the name according to you Xcode project
names = ['icon_1024.png', 'icon_180.png',
         'icon_167.png', 'icon_152.png',
         'icon_144_2.png', 'icon_144.png',
         'icon_120_2.png', 'icon_120.png',
         'icon_100.png', 'icon_87.png',
         'icon_80_2.png', 'icon_80.png',
         'icon_76.png', 'icon_72.png',
         'icon_60.png', 'icon_58_2.png',
         'icon_58.png', 'icon_57.png',
         'icon_50.png', 'icon_40_1.png',
         'icon_40_2.png', 'icon_40.png',
         'icon_29_2.png', 'icon_29.png',
         'icon_20.png']

# Set the icon size, width is equal to height
# The size corresponds to the icon name above
values = ['1024', '180',
          '167', '152',
          '144', '144',
          '120', '120',
          '100', '87',
          '80', '80',
          '76', '72',
          '60', '58',
          '58', '57',
          '50', '40',
          '40', '40',
          '29', '29',
          '20']


_iconnames = []
_iconsizes = []
_images = {}


def init(iconnames, iconsizes):
    _iconnames = iconnames
    _iconsizes = iconsizes
    for index, name in enumerate(_iconnames):
        _images[name] = _iconsizes[index]


def get_image_size(icon):
    im = Image.open(icon)
    return im.size[0]


def valid_image():
    '''
    if exist a lot of .png files in current path
    use the first element as the original image
    '''
    images = []
    for file_name in os.listdir(os.getcwd()):
        if file_name.endswith(".png"):
            size = get_image_size(file_name)
            # for iOS 11 , must contains the size 1024*1024
            # so the lastes size is 1024
            if size >= 1024:
                images.append(file_name)
    return images


def make_icon_dir(name):
    if os.path.isabs(name):
        path = name
    else:
        path = os.path.abspath(name)
    if not os.path.exists(path):
        os.mkdir(path)
    return path


def resize_image(imagename, size, new_path):
    im = Image.open(imagename)
    if isinstance(size, str):
        size = int(size)
    new_size = (size, size)
    # use `Image.ANTIALIAS` for the hightest quaility
    # otherwise , the icon may indistinct
    out = im.resize(new_size, Image.ANTIALIAS)
    out.save(new_path)


def run(folder=None):

    images = valid_image()

    if images:
        image = images[0]
        image_type_len = len(Image.open(image).split())
        # the icon must close alpha, if not ,can't upload to AppStore
        if image_type_len == 4:
            print "please close the [{0}] alpha channel".format(image)
            return
        print "Use [%s] as the original image" % image

        if not folder:
            folder = "output_icon"
        # create folder as the output path
        make_icon_dir(folder)

        print "begin resize image....."
        for new_path, size in _images.items():
            new_path = os.path.join(os.getcwd(), folder, new_path)
            resize_image(image, size, new_path)
        print "done....\n"

    else:
        raise Exception("\n\nError! Can't find valid image, size must be at least 1024*1024, please check again!!\n")


init(names, values)

run()
