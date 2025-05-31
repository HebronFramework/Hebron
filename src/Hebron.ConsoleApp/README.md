# Console Application

This is a console application for running Hebron.

## stb_image

These are the arguments used when generating code for `stb_image`.

### Namespace

`StbImageSharp`

### Class Name

`StbImage`

### Defines

* `STBI_NO_SIMD`
* `STBI_NO_PIC`
* `STBI_NO_PNM`
* `STBI_NO_STDIO`
* `STB_IMAGE_IMPLEMENTATION`

### Skip Global Variables

* `stbi__g_failure_reason`

### Skip Functions

* `stbi__err`
* `stbi_failure_reason`

### Replacements

* `stbi__jpeg j = (stbi__jpeg)(stbi__malloc((ulong)(sizeof(stbi__jpeg))))`
* `var j = new stbi__jpeg()`