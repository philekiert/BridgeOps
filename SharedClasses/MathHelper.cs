static class MathHelper
{
    public static void Clamp(ref float value, float minimum, float maximum)
    {
        if (value < minimum) value = minimum;
        else if (value > maximum) value = maximum;
    }
    public static void Clamp(ref double value, double minimum, double maximum)
    {
        if (value < minimum) value = minimum;
        else if (value > maximum) value = maximum;
    }

    public static void Lerp(ref float value, float target, float amount)
    {
        if (value < target)
        {
            value += ((target - value) * amount);
            if (value > target) value = target;
        }
        else if (value > target)
        {
            value += ((target - value) * amount);
            if (value < target) value = target;
        }
    }
    public static void Lerp(ref float value, float target, float amount, float minimum)
    {
        if (value < target)
        {
            float movement = ((target - value) * amount);
            value += movement > minimum ? movement : minimum;
            if (value > target) value = target;
        }
        else if (value > target)
        {
            float movement = ((target - value) * amount);
            value += movement < -minimum ? movement : -minimum;
            if (value < target) value = target;
        }
    }
    public static void Lerp(ref double value, double target, double amount)
    {
        if (value < target)
        {
            value += ((target - value) * amount);
            if (value > target) value = target;
        }
        else if (value > target)
        {
            value += ((target - value) * amount);
            if (value < target) value = target;
        }
    }
    public static void Lerp(ref double value, double target, double amount, double minimum)
    {
        if (value < target)
        {
            double movement = ((target - value) * amount);
            value += movement > minimum ? movement : minimum;
            if (value > target) value = target;
        }
        else if (value > target)
        {
            double movement = ((target - value) * amount);
            value += movement < -minimum ? movement : -minimum;
            if (value < target) value = target;
        }
    }
}