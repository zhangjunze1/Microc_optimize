(* File MicroC/Contcomp.fs
   A continuation-based (backwards) compiler from micro-C, a fraction of
   the C language, to an abstract machine.  
   sestoft@itu.dk * 2011-11-10

   The abstract machine code is generated backwards, so that jumps to
   jumps can be eliminated, so that tail-calls (calls immediately
   followed by return) can be recognized, dead code can be eliminated, 
   etc.

   The compilation of a block, which may contain a mixture of
   declarations and statements, proceeds in two passes:

   Pass 1: elaborate declarations to find the environment in which
           each statement must be compiled; also translate
           declarations into allocation instructions, of type
           bstmtordec.
  
   Pass 2: compile the statements in the given environments.
 *)

module Contcomp

open System.IO
open Absyn
open Machine

(* The intermediate representation between passes 1 and 2 above:  *)

type bstmtordec = // 变量声明 
     | BDec of instr list                  (* 局部变量声明 Declaration of local variable  *)
     | BStmt of stmt                       (* A statement                    *)

(* ------------------------------------------------------------------- *)

(* Code-generating functions that perform local optimizations *)

let rec addINCSP m1 C : instr list =
    match C with
    | INCSP m2            :: C1 -> addINCSP (m1+m2) C1
    | RET m2              :: C1 -> RET (m2-m1) :: C1
    | Label lab :: RET m2 :: _  -> RET (m2-m1) :: C
    | _                         -> if m1=0 then C else INCSP m1 :: C

let addLabel C : label * instr list =          (* Conditional jump to C *)
    match C with
    | Label lab :: _ -> (lab, C)
    | GOTO lab :: _  -> (lab, C)
    | _              -> let lab = newLabel() 
                        (lab, Label lab :: C)

let makeJump C : instr * instr list =          (* Unconditional jump to C *)
    match C with
    | RET m              :: _ -> (RET m, C)
    | Label lab :: RET m :: _ -> (RET m, C)
    | Label lab          :: _ -> (GOTO lab, C)
    | GOTO lab           :: _ -> (GOTO lab, C)
    | _                       -> let lab = newLabel() 
                                 (GOTO lab, Label lab :: C)

let makeCall m lab C : instr list =
    match C with
    | RET n            :: C1 -> TCALL(m, n, lab) :: C1
    | Label _ :: RET n :: _  -> TCALL(m, n, lab) :: C
    | _                      -> CALL(m, lab) :: C

let rec deadcode C =
    match C with
    | []              -> []
    | Label lab :: _  -> C
    | _         :: C1 -> deadcode C1

let addNOT C =
    match C with
    | NOT        :: C1 -> C1
    | IFZERO lab :: C1 -> IFNZRO lab :: C1 
    | IFNZRO lab :: C1 -> IFZERO lab :: C1 
    | _                -> NOT :: C

let addJump jump C =                    (* jump is GOTO or RET *)
    let C1 = deadcode C
    match (jump, C1) with
    | (GOTO lab1, Label lab2 :: _) -> if lab1=lab2 then C1 
                                      else GOTO lab1 :: C1
    | _                            -> jump :: C1
    
let addGOTO lab C =
    addJump (GOTO lab) C

let rec addCST i C =
    match (i, C) with
    | (0, ADD        :: C1) -> C1
    | (0, SUB        :: C1) -> C1
    | (0, NOT        :: C1) -> addCST 1 C1
    | (_, NOT        :: C1) -> addCST 0 C1
    | (1, MUL        :: C1) -> C1
    | (1, DIV        :: C1) -> C1
    | (0, EQ         :: C1) -> addNOT C1
    | (_, INCSP m    :: C1) -> if m < 0 then addINCSP (m+1) C1
                               else CSTI i :: C
    | (0, IFZERO lab :: C1) -> addGOTO lab C1
    | (_, IFZERO lab :: C1) -> C1
    | (0, IFNZRO lab :: C1) -> C1
    | (_, IFNZRO lab :: C1) -> addGOTO lab C1
    | _                     -> CSTI i :: C

let rec addCSTC i C =
    match (i, C) with
    | _                     -> (CSTC ((int32)(System.BitConverter.ToInt16(System.BitConverter.GetBytes(char(i)), 0)))) :: C

let rec addCSTS (s:string) C =
    match (s, C) with
    | _                     -> let mutable list =[];
                               for i = 0 to s.Length do
                                   list <- (int (s.Chars(i)))::list
                               (CSTS list) :: C
(* ------------------------------------------------------------------- *)

(* Simple environment operations *)

type 'data Env = (string * 'data) list

let rec lookup env x = 
    match env with 
    | []         -> failwith (x + " not found")
    | (y, v)::yr -> if x=y then v else lookup yr x

(* A global variable has an absolute address, a local one has an offset: *)

type Var = 
    | Glovar of int                   (* absolute address in stack           *) // 绝对地址
    | Locvar of int                   (* address relative to bottom of frame *) // 相对地址

(* The variable environment keeps track of global and local variables, and 
   keeps track of next available offset for local variables *)

type VarEnv = (Var * typ) Env * int

(* The function environment maps a function name to the function's label, 
   its return type, and its parameter declarations *)

type Paramdecs = (typ * string) list // 函数参数
type FunEnv = (label * typ option * Paramdecs) Env // 函数定义 名字 + 返回类型 + 函数参数

(* Bind declared variable in varEnv and generate code to allocate it: *)

let allocate (kind : int -> Var) (typ, x) (varEnv : VarEnv) : VarEnv * instr list =
    let (env, fdepth) = varEnv 
    match typ with
    | TypA (TypA _, _) -> failwith "allocate: arrays of arrays not permitted"
    | TypA (t, Some i) ->
      let newEnv = ((x, (kind (fdepth+i), typ)) :: env, fdepth+i+1)
      let code = [INCSP i; GETSP; CSTI (i-1); SUB]
      (newEnv, code)
    | TypS         ->
        let newEnv = ((x, (kind (fdepth+128), typ)) :: env, fdepth+128+1)
        let code = [INCSP 128; GETSP; CSTI (128-1); SUB]
        (newEnv, code)
    | _ -> 
      let newEnv = ((x, (kind (fdepth), typ)) :: env, fdepth+1)
      let code = [INCSP 1]
      (newEnv, code)

(* Bind declared parameter in env: *)

let bindParam (env, fdepth) (typ, x) : VarEnv = 
    ((x, (Locvar fdepth, typ)) :: env, fdepth+1);

let bindParams paras (env, fdepth) : VarEnv = 
    List.fold bindParam (env, fdepth) paras;

(* ------------------------------------------------------------------- *)

(* Build environments for global variables and global functions *)

let makeGlobalEnvs(topdecs : topdec list) : VarEnv * FunEnv * instr list = 
    let rec addv decs varEnv funEnv = 
        match decs with 
        | [] -> (varEnv, funEnv, [])
        | dec::decr -> 
          match dec with
          | Vardec (typ, x) ->
            let (varEnv1, code1) = allocate Glovar (typ, x) varEnv
            let (varEnvr, funEnvr, coder) = addv decr varEnv1 funEnv
            (varEnvr, funEnvr, code1 @ coder)
          | Fundec (tyOpt, f, xs, body) ->
            addv decr varEnv ((f, (newLabel(), tyOpt, xs)) :: funEnv)
    addv topdecs ([], 0) []
    
(* ------------------------------------------------------------------- *)

(* Compiling micro-C statements:

   * stmt    is the statement to compile
   * varenv  is the local and global variable environment 
   * funEnv  is the global function environment
   * C       is the code that follows the code for stmt
*)

let rec cStmt stmt (varEnv : VarEnv) (funEnv : FunEnv) (C : instr list) : instr list = 
    match stmt with
    | If(e, stmt1, stmt2) -> 
      let (jumpend, C1) = makeJump C
      let (labelse, C2) = addLabel (cStmt stmt2 varEnv funEnv C1)
      cExpr e varEnv funEnv (IFZERO labelse 
       :: cStmt stmt1 varEnv funEnv (addJump jumpend C2))
    | While(e, body) ->
      let labbegin = newLabel()
      let (jumptest, C1) = 
           makeJump (cExpr e varEnv funEnv (IFNZRO labbegin :: C))
      addJump jumptest (Label labbegin :: cStmt body varEnv funEnv C1)
    | DoWhile(body, e) ->
        let labbegin = newLabel()
        let C1 = 
            cExpr e varEnv funEnv (IFNZRO labbegin :: C)
        Label labbegin :: cStmt body varEnv funEnv C1 //先执行body
    | For (e1, e2, e3, body) ->
      let labend   = newLabel()                       //结束label
      let labbegin = newLabel()                       //设置label 
      let labope   = newLabel()                       //设置 for(,,opera) 的label
      let Cend = Label labend :: C
      let (jumptest, C1) =
        makeJump (cExpr e2 varEnv funEnv (IFNZRO labbegin :: Cend))
      let C2 = Label labope :: cExpr e3 varEnv funEnv (addINCSP -1 C1)
      let C3 = cStmt body varEnv funEnv C2    
      cExpr e1 varEnv funEnv (addINCSP -1 (addJump jumptest  (Label labbegin :: C3) ) )
    | Expr e -> 
      cExpr e varEnv funEnv (addINCSP -1 C) 
    | Block stmts -> 
      let rec pass1 stmts ((_, fdepth) as varEnv) =
          match stmts with 
          | []     -> ([], fdepth)
          | s1::sr ->
            let (_, varEnv1) as res1 = bStmtordec s1 varEnv
            let (resr, fdepthr) = pass1 sr varEnv1 
            (res1 :: resr, fdepthr) 
      let (stmtsback, fdepthend) = pass1 stmts varEnv
      let rec pass2 pairs C = 
          match pairs with 
          | [] -> C
          | (BDec code,  varEnv) :: sr -> code @ pass2 sr C
          | (BStmt stmt, varEnv) :: sr -> cStmt stmt varEnv funEnv (pass2 sr C)
      pass2 stmtsback (addINCSP(snd varEnv - fdepthend) C)
    | Myctrl ctrl ->
            match ctrl with
            | Return x  -> 
                if x.IsSome then 
                  cExpr x.Value varEnv funEnv (RET (snd varEnv) :: deadcode C)
                else 
                  RET (snd varEnv - 1) :: deadcode C
            | _         -> RET (snd varEnv - 1) :: deadcode C
    // | Return None -> 
    //   RET (snd varEnv - 1) :: deadcode C
    // | Return (Some e) -> 
    //   cExpr e varEnv funEnv (RET (snd varEnv) :: deadcode C)

and bStmtordec stmtOrDec varEnv : bstmtordec * VarEnv =
    match stmtOrDec with 
    | Stmt stmt    ->
      (BStmt stmt, varEnv) 
    | Dec (typ, x) ->
      let (varEnv1, code) = allocate Locvar (typ, x) varEnv 
      (BDec code, varEnv1)
    | DecAndAssign (typ,x,e) -> 
      let (varEnv1, code) = allocate Locvar (typ,x) varEnv
      (BDec (cAccess (AccVar(x)) varEnv1 []  // cAccess 增加 x变量 对应的 的指令
                (cExpr e varEnv1 [] (STI :: (addINCSP -1 code))) // 取出这个变量 给他赋值 
            ), varEnv1)
(* Compiling micro-C expressions: 

   * e       is the expression to compile 编译的表达式
   * varEnv  is the compile-time variable environment 编译时变量环境
   * funEnv  is the compile-time environment 编译时环境
   * C       is the code following the code for this expression 表达式代码后面的代码

   Net effect principle: if the compilation (cExpr e varEnv funEnv C) of
   expression e returns the instruction sequence instrs, then the
   execution of instrs will have the same effect as an instruction
   sequence that first computes the value of expression e on the stack
   top and then executes C, but because of optimizations instrs may
   actually achieve this in a different way.
 *)

and cExpr (e : expr) (varEnv : VarEnv) (funEnv : FunEnv) (C : instr list) : instr list =
    match e with
    | Access acc     -> cAccess acc varEnv funEnv (LDI :: C)
    | Assign(acc, e) -> cAccess acc varEnv funEnv (cExpr e varEnv funEnv (STI :: C))
    | CstI i         -> addCST i C
    | ConstChar i    -> addCST (int i) C   //字符
    | ConstString s     -> addCST (int s) C     //字符串
    | Addr acc       -> cAccess acc varEnv funEnv C
    | Print(ope,e1)  -> // print("%d",i)
      cExpr e1 varEnv funEnv
           (match ope with
            | "%d"  -> PRINTI :: C
            | "%c"  -> PRINTC :: C
            | "%s"  -> PRINTC :: C
            | _        -> failwith "unknown primitive 1")
    | Prim1(ope, e1) ->
      cExpr e1 varEnv funEnv
          (match ope with
           | "!"      -> addNOT C
           | "printi" -> PRINTI :: C
           | "printc" -> PRINTC :: C
           | _        -> failwith "unknown primitive 1")
    | Prim2(ope, e1, e2) ->
      cExpr e1 varEnv funEnv
        (cExpr e2 varEnv funEnv
           (match ope with
            | "*"   -> MUL  :: C
            | "+"   -> ADD  :: C
            | "-"   -> SUB  :: C
            | "/"   -> DIV  :: C
            | "%"   -> MOD  :: C
            | "=="  -> EQ   :: C
            | "!="  -> EQ   :: addNOT C
            | "<"   -> LT   :: C
            | ">="  -> LT   :: addNOT C
            | ">"   -> SWAP :: LT :: C
            | "<="  -> SWAP :: LT :: addNOT C
            | _     -> failwith "unknown primitive 2"))
    | Prim3 (e1, e2, e3) ->
      let (jumpend, C1) = makeJump C
      let (labelse, C2) = addLabel (cExpr e3 varEnv funEnv C1)
      cExpr e1 varEnv funEnv (IFZERO labelse 
      :: cExpr e2 varEnv funEnv (addJump jumpend C2))
    | SimpleOpt(ope,acc,e)->             
        cExpr e varEnv funEnv  
            (match ope with
            | "+=" -> 
                let ass = Assign (acc,Prim2("+",Access acc, e))
                cExpr ass varEnv funEnv (addINCSP -1 C)
            | "-=" ->
                let ass = Assign (acc,Prim2("-",Access acc, e))
                cExpr ass varEnv funEnv (addINCSP -1 C)
            | "++Z" -> 
                let ass = Assign (acc,Prim2("+",Access acc, e))
                let C1 = cExpr ass varEnv funEnv C
                CSTI 1 :: ADD :: (addINCSP -1 C1)
            | "Z++" -> 
                let ass = Assign (acc,Prim2("+",Access acc, e))
                let C1 = cExpr ass varEnv funEnv C
                CSTI 1 :: ADD :: (addINCSP -1 C1)
            | "--Z" ->
                let ass = Assign (acc,Prim2("-",Access acc, e))
                let C1 = cExpr ass varEnv funEnv C
                CSTI 1 :: SUB :: (addINCSP -1 C1)  
            | "Z--" ->
                let ass = Assign (acc,Prim2("-",Access acc, e))
                let C1 = cExpr ass varEnv funEnv C
                CSTI 1 :: SUB :: (addINCSP -1 C1)      
            | "*=" -> 
                let ass = Assign (acc,Prim2("*",Access acc, e))
                cExpr ass varEnv funEnv (addINCSP -1 C)
            | "/=" ->
                let ass = Assign (acc,Prim2("/",Access acc, e))
                cExpr ass varEnv funEnv (addINCSP -1 C)
            | "%=" ->
                let ass = Assign (acc,Prim2("%",Access acc, e))
                cExpr ass varEnv funEnv (addINCSP -1 C)
            | _         -> failwith "Error: unknown unary operator")
    | Andalso(e1, e2) ->
      match C with
      | IFZERO lab :: _ ->
         cExpr e1 varEnv funEnv (IFZERO lab :: cExpr e2 varEnv funEnv C)
      | IFNZRO labthen :: C1 -> 
        let (labelse, C2) = addLabel C1
        cExpr e1 varEnv funEnv
           (IFZERO labelse 
              :: cExpr e2 varEnv funEnv (IFNZRO labthen :: C2))
      | _ ->
        let (jumpend,  C1) = makeJump C
        let (labfalse, C2) = addLabel (addCST 0 C1)
        cExpr e1 varEnv funEnv
          (IFZERO labfalse 
             :: cExpr e2 varEnv funEnv (addJump jumpend C2))
    | Orelse(e1, e2) -> 
      match C with
      | IFNZRO lab :: _ -> 
        cExpr e1 varEnv funEnv (IFNZRO lab :: cExpr e2 varEnv funEnv C)
      | IFZERO labthen :: C1 ->
        let(labelse, C2) = addLabel C1
        cExpr e1 varEnv funEnv
           (IFNZRO labelse :: cExpr e2 varEnv funEnv
             (IFZERO labthen :: C2))
      | _ ->
        let (jumpend, C1) = makeJump C
        let (labtrue, C2) = addLabel(addCST 1 C1)
        cExpr e1 varEnv funEnv
           (IFNZRO labtrue 
             :: cExpr e2 varEnv funEnv (addJump jumpend C2))
    | Call(f, es) -> callfun f es varEnv funEnv C

(* Generate code to access variable, dereference pointer or index array: *)

and cAccess access varEnv funEnv C = 
    match access with 
    | AccVar x   ->
      match lookup (fst varEnv) x with
      | Glovar addr, _ -> addCST addr C
      | Locvar addr, _ -> GETBP :: addCST addr (ADD :: C)
    | AccDeref e ->
      cExpr e varEnv funEnv C
    | AccIndex(acc, idx) ->
      cAccess acc varEnv funEnv (LDI :: cExpr idx varEnv funEnv (ADD :: C))

(* Generate code to evaluate a list es of expressions: *)

and cExprs es varEnv funEnv C = 
    match es with 
    | []     -> C
    | e1::er -> cExpr e1 varEnv funEnv (cExprs er varEnv funEnv C)

(* Generate code to evaluate arguments es and then call function f: *)
    
and callfun f es varEnv funEnv C : instr list =
    let (labf, tyOpt, paramdecs) = lookup funEnv f
    let argc = List.length es
    if argc = List.length paramdecs then
      cExprs es varEnv funEnv (makeCall argc labf C)
    else
      failwith (f + ": parameter/argument mismatch")

(* Compile a complete micro-C program: globals, call to main, functions *)

let cProgram (Prog topdecs) : instr list = 
    let _ = resetLabels ()
    let ((globalVarEnv, _), funEnv, globalInit) = makeGlobalEnvs topdecs
    let compilefun (tyOpt, f, xs, body) =
        let (labf, _, paras) = lookup funEnv f
        let (envf, fdepthf) = bindParams paras (globalVarEnv, 0)
        let C0 = [RET (List.length paras-1)]
        let code = cStmt body (envf, fdepthf) funEnv C0
        Label labf :: code
    let functions = 
        List.choose (function 
                         | Fundec (rTy, name, argTy, body) 
                                    -> Some (compilefun (rTy, name, argTy, body))
                         | Vardec _ -> None)
                         topdecs
    let (mainlab, _, mainparams) = lookup funEnv "main"
    let argc = List.length mainparams
    globalInit 
    @ [LDARGS argc; CALL(argc, mainlab); STOP] 
    @ List.concat functions

(* Compile the program (in abstract syntax) and write it to file
   fname; also, return the program as a list of instructions.
 *)

let intsToFile (inss : int list) (fname : string) = 
    File.WriteAllText(fname, String.concat " " (List.map string inss))

let contCompileToFile program fname = 
    let instrs   = cProgram program 
    let bytecode = code2ints instrs
    intsToFile bytecode fname; instrs

(* Example programs are found in the files ex1.c, ex2.c, etc *)
