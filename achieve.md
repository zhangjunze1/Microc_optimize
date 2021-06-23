# 上手编译原理大作业

## 完成情况
+ |* *| 注释


## 解释器部分--interpreter

+ Absyn.fs    抽象语法类 与实际实践并无关联 （可写可不写）
+ CLex.fsl    词法分析器--
	+ 例如定义 for 等关键字
+ CPar.fsy   语法分析器--
	+ 例如 定义do while 进行使用的时候 （while已经作为关键字使用过 所以你需要利用语法定义实现do while）
+ Parse.fs 最好不要动 语法解析器-- 词法分析程序--语法分析程序（没必要动）
+ Interp.fs 最好不要动 （没必要）
+ interpc.fsproj 此文档是项目调用的fs文档等等 


## 解释器具体流程
+ 写CLex.fsl 词法分析器
+ 写CPar.fsy 语法分析器

```
# 编译解释器 interpc.exe 命令行程序 
dotnet restore  interpc.fsproj   # 可选
dotnet clean  interpc.fsproj     # 可选
dotnet build -v n interpc.fsproj # 构建./bin/Debug/net5.0/interpc.exe ，-v n查看详细生成过程

# 执行解释器
./bin/Debug/net5.0/interpc.exe ex1.c 8
dotnet run -p interpc.fsproj ex1.c 8
dotnet run -p interpc.fsproj -g ex1.c 8  //显示token AST 等调试信息

# one-liner 
# 自行修改 interpc.fsproj  依次解释多个源文件
dotnet build -t:ccrun interpc.fsproj 
```
+ dotnet命令行fsi中运行解释器
```
# 命令行运行程序
dotnet fsi 

#r "nuget: FsLexYacc";;  //添加包引用 必须要的
#load "Absyn.fs" "Debug.fs" "CPar.fs" "CLex.fs" "Parse.fs" "Interp.fs" "ParseAndRun.fs" ;; // 需要的包的名字

open ParseAndRun;;    //导入模块 ParseAndRun
fromFile "example\ex1.c";;    //显示 ex1.c的语法树 // 写好的.c文件
run (fromFile "example\ex1.c") [17];; //解释执行 ex1.c
run (fromFile "example\ex11.c") [8];; //解释执行 ex11.c

Debug.debug <-  true  //打开调试

run (fromFile "example\ex1.c") [8];; //解释执行 ex1.c
run (fromFile "example\ex11.c") [8];; //解释执行 ex11.
#q;;
```
## 完整运行步骤
```
第一部分是运行把.c文件在解释器的环境下跑一遍得到结果---不通过dotnet fsi
=======================================================================
dotnet restore  interpc.fsproj   
dotnet clean  interpc.fsproj     
dotnet build -v n interpc.fsproj --解释器重新构建// 这个部分就是把你的 词法分析器和词法分析器新写的部分覆盖掉原来的部分

dotnet run -p interpc.fsproj example/ex1.c 8   // 此处 example/ex1.c 这个部分是你要运行的.c文件  // 8指的是这个文件需要读入的数
// 可以不用的 dotnet build -t:ccrun interpc.fsproj  //自行修改 interpc.fsproj  依次解释多个源文件

=======================================================================
第二部分是通过命令行运行 可以查看语法树 可以机型解释执行 可以进行debug调试  // 拿example/ex1.c 举例 
=======================================================================
dotnet restore  interpc.fsproj   
dotnet clean  interpc.fsproj     
dotnet build -v n interpc.fsproj --解释器重新构建// 这个部分就是把你的 词法分析器和词法分析器新写的部分覆盖掉原来的部分
// 进入ploofs/microc 文件夹下
dotnet fsi  // 进入命令行运行程序

#r "nuget: FsLexYacc";;  //添加包引用 ----#号不要去掉---- 调用编译器的基本的包
#load "Absyn.fs" "Debug.fs" "CPar.fs" "CLex.fs" "Parse.fs" "Interp.fs" "ParseAndRun.fs" ;; // ---调用你写的词法分析器 语法分析器等等.fs文件

open ParseAndRun;;    // 导入模块 ParseAndRun
fromFile "example\ex1.c";;    // 显示 ex1.c的语法树
run (fromFile "example\ex1.c") [17];; //解释执行 ex1.c

Debug.debug <-  true  //打开调试 如果上一步运行错误可以通过debug的模式进行判断
run (fromFile "example\ex1.c") [8];; //解释执行 ex1.c

#q;; // 退出
```





## 编译器部分--compiler 

**PS: 解释器做好的功能 编译器一样能用 只要你不动Interp.fs （把.c文件变成.out 机器代码的部分）**
**PS: 编译器的部分的machine.java就是用于反编译 把.out文件 反编译之后运行得出结果**

+ Machine.fs （基本不用改）--汇编指令的添加
+ 选择使用的语言进行反编译
	1. Machine.java                    VM 实现 java
	2. machine.c                          VM 实现 c 
	3. machine.cs                  VM 实现 c#
+ machine.csproj                           VM 项目文件
+ microc.fsproj 编译器项目文件
+ .....省略

## 编译器完整运行步骤 

### 第一部分：开发后 
+ 开发的时候主要用第二部分
+ 开发以后通过编译器运行.c文件的时候需要用第一部分
```
第一部分是运行把.c文件在解释器的环境下跑一遍得到结果---不通过dotnet fsi
=======================================================================
dotnet restore  microc.fsproj   
dotnet clean  microc.fsproj   
dotnet build  microc.fsproj  --编译器重新构建// 这个部分就是把你的 词法分析器和词法分析器新写的部分覆盖掉原来的部分 然后导入.fs文件
=======================================================================
dotnet run -p microc.fsproj example/ex1.c  // 此处 example/ex1.c 这个部分是你要运行的.c文件  // 8指的是这个文件需要读入的数
dotnet run -p microc.fsproj -g example/ex1.c // -g 查看调试信息
=======================================================================
// dotnet built -t:ccrun microc.fsproj     # 编译并运行 example 目录下多个文件 可以不用
// dotnet built -t:cclean microc.fsproj    # 清除生成的文件
```

### 第二、三部分：开发时
```
第二部分是通过命令行运行 可以机型编译执行 生成.out文件  // 拿mytest.c 举例 
=======================================================================
dotnet restore  microc.fsproj   
dotnet clean  microc.fsproj   
dotnet build  microc.fsproj  --编译器重新构建// 这个部分就是把你的 词法分析器和词法分析器新写的部分覆盖掉原来的部分 然后导入.fs文件
=======================================================================
// 进入ploofs/microc 文件夹下
dotnet fsi  // 进入命令行运行程序
=======================================================================
#r "nuget: FsLexYacc";; //添加包引用 ----#号不要去掉---- 调用编译器的基本的包
#load "Absyn.fs"  "CPar.fs" "CLex.fs" "Debug.fs" "Parse.fs" "Machine.fs" "Backend.fs" "Comp.fs" "ParseAndComp.fs";;  // ---调用你写的词法分析器 语法分析器等等.fs文件
=======================================================================
// 运行编译器
open ParseAndComp;;   // 导入模块 ParseAndRun
compileToFile (fromFile "mytest.c") "mytest";;  // 生成了对应的mytest.out文件

// 生成的.out文件如下 机器代码
```

![image-20210623110146083](http://zhangjz-tgam-example.oss-cn-hangzhou.aliyuncs.com/activity/uUNIwEFVfZ.jpg?Expires=1624449216&OSSAccessKeyId=LTAI5tBji2779oNNiitohXS7&Signature=UousUmMacPulIws17mVEt99MARs%3D)

```
第三部分衔接第二部分 // 虚拟机构建与运行
=======================================================================
// 回到文件目录 ctrl+D 退出dotnet  拿mytest.out举例 
=======================================================================
// 运行虚拟机 在dotnet环境下
dotnet clean  machine.csproj
dotnet run -p machine.csproj mytest.out 3 # 运行虚拟机，执行 mytest.out
./bin/Debug/net5.0/machine.exe -t ex9.out 0 
=======================================================================
// 用写好的machine.c 用java环境 （我是用java环境）
javac Machine.java // 编译生成新的class文件
java Machine Mytest.out 3

---即可验证是否成功

```

