// See https://aka.ms/new-console-template for more information

MegaLisp.Engine engine = new();
var r = engine.Eval("(* 3 (+ 2 2))");
var c = engine.Eval("(car (cons (cons 4 5) 2))");
var c2 = engine.Eval("(cdr (cons (cons 4 5) 2))");
engine.Eval("(defun test (x y) (+ x y))"); 