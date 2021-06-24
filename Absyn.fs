module Absyn

// 基本类型
// 注意，数组、指针是递归类型
// 这里没有函数类型
type typ =
  | TypI                             (* Type int                    *)
  | TypC                             (* Type char                   *)
  | TypeFloat                        (* Type float                   *)
  | TypA of typ * int option         (* Array type                  *)
  | TypS                             (* Type string                *)
  | TypP of typ                      (* Pointer type                *)
                                                                   
and expr =                           // 表达式，右值                                                
  | Access of access                 (* x    or  *p    or  a[e]     *) //访问左值（右值）
  | Assign of access * expr          (* x=e  or  *p=e  or  a[e]=e   *)
  | Addr of access                   (* &x   or  &*p   or  &a[e]    *)
  | CstI of int                      (* Constant                    *)
  | ConstChar of char                (*constant char*) 
  | ConstString of string            (*constant string*)
  | ConstFloat of float32            (*constant float*) // Zhangjz
  | Print of string * expr           
  | Prim1 of string * expr           (* Unary primitive operator    *)
  | Prim2 of string * expr * expr    (* Binary primitive operator   *)
  | Andalso of expr * expr           (* Sequential and              *)
  | Orelse of expr * expr            (* Sequential or               *)
  | Call of string * expr list       (* Function call f(...)        *)
                                                            
and access =                         //左值，存储的位置                                            
  | AccVar of string                 (* Variable access        x    *) 
  | AccDeref of expr                 (* Pointer dereferencing  *p   *)
  | AccIndex of access * expr        (* Array indexing         a[e] *)
                                                                   
and stmt =                                                         
  | If of expr * stmt * stmt         (* Conditional                 *)
  | While of expr * stmt             (* While loop                  *)
  | For of expr * expr * expr * stmt (* normal for *)
  | Expr of expr                     (* Expression statement   e;   *)
  | Return of expr option            (* Return from method          *)
  | Block of stmtordec list          (* Block: grouping and scope   *)
  // 语句块内部，可以是变量声明 或语句的列表                                                              

and stmtordec =                                                    
  | Dec of typ * string              (* Local variable declaration  *)
  | Stmt of stmt                     (* A statement                 *)
  | DecAndAssign of typ * string * expr

// 顶级声明 可以是函数声明或变量声明
and topdec = 
  | Fundec of typ option * string * (typ * string) list * stmt
  | Vardec of typ * string
  | VardecAndAssignment of typ * string * expr

// 程序是顶级声明的列表
and program = 
  | Prog of topdec list
