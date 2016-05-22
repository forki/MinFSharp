﻿namespace MinFSharp


module Typing =
    open Chessie.ErrorHandling
    open Chessie.ErrorHandling.Trial
    type TypeMismatch = {expected:Type.t; actual: Type.t}
    type TypingError =
        | UnknownError
        | UnknownSymbol of Identifier.t
        | TypeMismatch of TypeMismatch
    let typeMismatch (exp:Type.t) (act:Type.t) =
            TypingError.TypeMismatch {expected = exp; actual = act}
    type TypingResult = Result<Type.t, TypingError>
    type TypedAstResult<'U> = Result<Syntax.t<'U> * Type.t, TypingError>

    let rec typed<'U> (env:Env.t<'U>) x : TypedAstResult<'U> =
        match x with
        | Syntax.Unit -> ok (x, Type.Unit)
        | Syntax.Bool(_) -> ok (x, Type.Bool)
        | Syntax.Int(_) -> ok (x, Type.Int)
        | Syntax.Float(_) -> ok (x, Type.Float)
        | Syntax.BinOp(op, a, b) ->
            trial {
                let! a, tya = typed env a
                let! b, tyb = typed env b
                let opId = Syntax.opName op |> Identifier.Id
                match Map.tryFind opId env with
                | None -> return! fail (UnknownSymbol opId)
                | Some o ->
                    let! _top, tyop = typed env o
                    match tyop with
                    | Type.Fun(atya, Type.Fun(atyb, tret)) when atya = tya && atyb = tyb ->
                        return Syntax.BinOp(op, a, b), tret
                    | Type.Fun(atya, Type.Fun(atyb, tret)) when atya <> tya || atyb <> tyb ->
                        if atya <> tya then
                            return! fail (typeMismatch atya tya)
                        else
                            return! fail (typeMismatch atyb tyb)
                    | Type.Fun(_args,_ret) -> return! fail (TypeMismatch {expected=Type.Var None; actual=tyop})
                    | _ -> return! fail (typeMismatch (Type.arrow [tya; tyb; Type.Var None]) tyop)
            }
        | Syntax.LetIn(_, _, _) -> failwith "Not implemented yet"
        | Syntax.If(cond, ethen, eelse) ->
            trial {
                let! cond, tcond = typed env cond
                if tcond <> Type.Bool then return! fail <| typeMismatch Type.Bool tcond
                let! ethen, tthen = typed env ethen
                let! eelse, telse = typed env eelse
                if tthen <> telse then return! fail <| typeMismatch tthen telse
                return Syntax.If(cond, ethen, eelse), tthen
            }
        | Syntax.Var(v) ->
            match Map.tryFind v env with
            | None -> fail (UnknownSymbol v)
            | Some vd -> typed env vd
        | Syntax.FunDef(args, body, ret) ->
            ok (x, Type.arrow((args |> List.map snd) @ [ret]))
//            trial {
//                let! tr, tyr = typed env body
//            }
        | Syntax.App(func, args) ->
            let rec typeArrow f tf args =
                trial {
                    match tf, args with
                    | Type.Fun(_, _), [] -> return f,tf
                    | Type.Fun(x, y), h::t when x = h -> return! typeArrow f y t
                    | Type.Fun(x, _), h::_ when x <> h -> return! fail <| typeMismatch x h
                    | t, [] -> return f, t
                    | _ -> return! fail UnknownError
                }
            trial {
                let! func, tfunc = typed env func
                let! typedArgs = args |> List.map (typed env) |> Trial.collect
                let args, targs = List.unzip typedArgs
                printfn "%A" targs
                return! typeArrow func tfunc targs
//                match tfunc with
//                | Type.Fun(fargs, fret) when fargs.Length >= targs.Length ->
//                    if Seq.zip targs fargs |> Seq.toList |> List.forall (fun (a,b) -> a = b)
//                    then return Syntax.App(func, args), fret
//                    else return! fail <| typeMismatch (Type.Fun(targs, Type.Var None)) tfunc
////                | Type.Fun(fargs, fret) ->
////                    return Syntax.App(func, args), fret
//                | _ -> return! fail <| typeMismatch (Type.Fun(targs, Type.Var None)) tfunc
//                return! fail UnknownError
            }
        | Syntax.Seq(_) -> failwith "Not implemented yet"
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
        | Syntax.FunDef(args, Syntax.FBody.Ext body, _ret) -> fail UnknownError
        | Syntax.FunDef(args, Syntax.FBody.Body body, _ret) ->
            trial {
                let targs = args |> List.map (snd)

                let! tvody = typing env body
                return Type.arrowr targs tvody
            }
        | Syntax.App(_, _) -> failwith "Not implemented yet"
        | Syntax.Seq l -> l |> List.map (typing env) |> Trial.collect |> Trial.bind (Seq.last >> ok)