namespace MinFSharp

module Type =
    type t =
    | Unit
    | Bool
    | Int
    | Float
    | Fun of t list * t (* arguments are uncurried *)
    | Tuple of t list
    | Array of t
    | Var of t option
    with
        override x.ToString() =
            let tstr x = x.ToString()
            match x with
            | Fun(l, r) -> l @ [r] |> List.map tstr |> String.concat " -> "
            | Tuple(l) -> l |> List.map tstr |> String.concat " * "
            | Array(t) -> sprintf "%O array" t
            | Var(t) -> sprintf "'%O" t
            | _ -> sprintf "%A" x

module Identifier =
    type t = Id of string

module Syntax =

    [<CustomEquality;NoComparison>]
    type FBody<'U> = | Body of t<'U> | Ext of (t<'U> list -> t<'U>)
    with
        override x.Equals(_yobj) =
            true //TODO: FIXME
        override x.GetHashCode() = 0
//    and Op = Lt | Gt | Eq | Ne
    and Op = string
    and t<'U> =
    | Unit
    | Bool of bool
    | Int of int
    | Float of float
    | BinOp of Op * t<'U> * t<'U>
//    | Let of (Identifier.t * Type.t) * t<'U>
    | LetIn of (Identifier.t * Type.t) * t<'U> * (t<'U> option)
    | If of t<'U> * t<'U> * t<'U>
    | Var of Identifier.t
    | FunDef of (Identifier.t * Type.t) list * FBody<'U>
    | App of t<'U> * t<'U> list
    | Seq of t<'U> list
    with
        override x.ToString() = sprintf "%A" x

    let appId s args = App(Var(Identifier.Id s), args)

    let map f s = 
        match s with
        | Unit | Bool(_) | Int(_) | Float(_) | Var(_) -> f s
        | BinOp(op, l, r) -> f (BinOp(op, f l, f r))
        | LetIn((id,t), eval, ein) -> f (LetIn((id, t), f eval, Option.map f ein))
        | If(cond, ethen, eelse) -> f (If(f cond, f ethen, f eelse))
        | FunDef(args, Body body) -> f(FunDef(args, f body |> Body))
        | FunDef(args, Ext ext) -> f(FunDef(args, Ext ext))
        | App(fu, args) -> f(App(f fu, args |> List.map f))
        | Seq stmts -> f(Seq(stmts |> List.map f))

module Env =
    open Identifier
    type t<'U> = Map<Identifier.t,Syntax.t<'U>>
    let newEnv<'U> =
        [(Id "add"), (Syntax.FunDef([Id "x",Type.Int; Id "y", Type.Int],
                                        Syntax.Ext(fun [Syntax.Int x; Syntax.Int y] -> Syntax.Int (x+y))))
         (Id "(+)", Syntax.Var(Id "add"))
        ] |> Map.ofList

module Typing =
    open Chessie.ErrorHandling
    type TypingError = UnknownSymbol of Identifier.t | TypeMismatch
    type TypingResult = Result<Type.t, TypingError>
    let rec typing env a : TypingResult =
        match a with
        | Syntax.Unit -> ok Type.Unit
        | Syntax.Bool(_) -> ok Type.Bool
        | Syntax.Int(_) -> ok Type.Int
        | Syntax.Float(_) -> ok Type.Float
        | Syntax.LetIn((_vid,_), vval,None) ->  typing env vval
        | Syntax.LetIn((vid,_), vval, Some e) ->
            trial {
                let! tVal = typing env vval
                let nEnv = env |> Map.add vid tVal
                return! typing nEnv e
            }
        | Syntax.Var(vid) ->
            Map.tryFind vid env |> failIfNone (UnknownSymbol(vid))
        | Syntax.FunDef(args, Syntax.FBody.Ext body) -> fail TypeMismatch
        | Syntax.FunDef(args, Syntax.FBody.Body body) ->
            trial {
                let targs = args |> List.map (snd)
                
                let! tvody = typing env body
                return Type.Fun(targs, tvody)
            }
        | Syntax.App(_, _) -> failwith "Not implemented yet"
        | Syntax.Seq l -> l |> List.map (typing env) |> Trial.collect |> Trial.bind (Seq.last >> ok)

module Interpreter =
    open Syntax
    open Chessie.ErrorHandling
    open Chessie.ErrorHandling.Trial
    type EvalError = | AppNotFound of Identifier.t | OpNotFound of string | ApplyNotFunction
    type EvalResult<'U> = Result<Syntax.t<'U>,EvalError>
    let rec eval<'U> (e:Env.t<'U>)(a:Syntax.t<'U>) : EvalResult<'U> =
        match a with
        | Unit -> ok Unit
        | Bool(_) | Int(_) | Float(_) -> ok a
        | LetIn((id,_ty), value, Some body) -> eval (e |> Map.add id value) body
        | Var(id) ->
            trial {
                let! def = (Map.tryFind id e) |> failIfNone (AppNotFound id)
                return! eval e def
            }
        | App(fid, fparams) ->
            match eval e fid with
            | Ok ((FunDef (fargs, fbody)), _) ->
                match fbody with
                | Body b ->
                    let ne = List.zip fargs fparams |> List.fold (fun env ((ai,_aty),fp) -> Map.add ai fp env) e
                    eval ne b
                | Ext ext -> ok <| ext fparams
            | Ok _ -> fail ApplyNotFunction
            | Bad(_e) -> fail _e.Head
        | FunDef(_fargs, _body) -> ok a
        | BinOp(op, l, r) ->
            let oid = (Identifier.Id <| sprintf "(%s)" op)
            eval e (App(Var oid, [l; r]))
        | LetIn(_, _, _) -> failwith "Not implemented yet"
        | If(eif, ethen, eelse) ->
            trial {
                let! rif = eval e eif
                return! if rif = Bool true then eval e ethen else eval e eelse
            }
        | Seq(_) -> failwith "Not implemented yet"
        //| _ -> failwith "Not implemented yet"