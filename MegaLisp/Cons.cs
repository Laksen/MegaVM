namespace MegaLisp;

internal class Cons
{
    public object Car;
    public object Cdr;

    public Cons(object car, object cdr)
    {
        Car = car;
        Cdr = cdr;
    }
}