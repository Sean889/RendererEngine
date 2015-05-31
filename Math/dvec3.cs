using System;

namespace GraphicsMath
{
    using val_ty = System.Double;
    struct dvec3
    {
        public val_ty x;
        public val_ty y;
        public val_ty z;

        public dvec3(val_ty x = 0, val_ty y = 0, val_ty z = 0)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public dvec3(dvec3 o)
        {
            this.x = o.x;
            this.y = o.y;
            this.z = o.z;
        }

        public static dvec3 operator +(dvec3 rhs, dvec3 lhs)
        {
            return new dvec3(rhs.x + lhs.x, rhs.y + lhs.y, rhs.z + lhs.z);
        }
        public static dvec3 operator -(dvec3 rhs, dvec3 lhs)
        {
            return new dvec3(rhs.x - lhs.x, rhs.y - lhs.y, rhs.z - lhs.z);
        }
        public static dvec3 operator *(dvec3 rhs, dvec3 lhs)
        {
            return new dvec3(rhs.x * lhs.x, rhs.y * lhs.y, rhs.z * lhs.z);
        }
        public static dvec3 operator /(dvec3 rhs, dvec3 lhs)
        {
            return new dvec3(rhs.x / lhs.x, rhs.y / lhs.y, rhs.z / lhs.z);
        }

        public static dvec3 operator +(dvec3 rhs, val_ty lhs)
        {
            return new dvec3(rhs.x + lhs, rhs.y + lhs, rhs.z + lhs);
        }
        public static dvec3 operator -(dvec3 rhs, val_ty lhs)
        {
            return new dvec3(rhs.x - lhs, rhs.y - lhs, rhs.z - lhs);
        }
        public static dvec3 operator *(dvec3 rhs, val_ty lhs)
        {
            return new dvec3(rhs.x * lhs, rhs.y * lhs, rhs.z * lhs);
        }
        public static dvec3 operator /(dvec3 rhs, val_ty lhs)
        {
            return new dvec3(rhs.x / lhs, rhs.y / lhs, rhs.z / lhs);
        }

        public static val_ty dot(dvec3 rhs, dvec3 lhs)
        {
            return rhs.x * lhs.x + rhs.y * lhs.y + rhs.z * lhs.z;
        }
        public static dvec3 cross(dvec3 rhs, dvec3 lhs)
        {
            return new dvec3(
                rhs.y * lhs.z - rhs.z * lhs.y,
                rhs.z * lhs.x - rhs.x * lhs.z,
                rhs.x * lhs.y - rhs.y * lhs.x);
        }
        public static dvec3 lerp(dvec3 a, dvec3 b, val_ty f)
        {
            return new dvec3(
                (a.x * (1.0f - f)) + (b.x * f),
                (a.y * (1.0f - f)) + (b.y * f),
                (a.z * (1.0f - f)) + (b.z * f));
        }

        public static dvec3 normalized(dvec3 lhs)
        {
            return lhs * (1 / System.Math.Sqrt(lhs.x * lhs.x + lhs.y * lhs.y + lhs.z * lhs.z));
        }

        public static val_ty length2(dvec3 lhs)
        {
            return lhs.x * lhs.x + lhs.y * lhs.y + lhs.z * lhs.z;
        }
        public static val_ty length(dvec3 lhs)
        {
            return System.Math.Sqrt(length2(lhs));
        }

        public static val_ty distance(dvec3 rhs, dvec3 lhs)
        {
            return length(rhs - lhs);
        }
        public static val_ty distance2(dvec3 rhs, dvec3 lhs)
        {
            return length2(rhs - lhs);
        }
        public static dvec3 abs(dvec3 lhs)
        {
            return lhs.abs();
        }

        public val_ty dot(dvec3 lhs)
        {
            return dot(this, lhs);
        }
        public dvec3 cross(dvec3 lhs)
        {
            return cross(this, lhs);
        }
        public dvec3 lerp(dvec3 lhs, val_ty f)
        {
            return lerp(this, lhs, f);
        }

        public val_ty length()
        {
            return length(this);
        }
        public val_ty length2()
        {
            return length2(this);
        }

        public val_ty distance(dvec3 lhs)
        {
            return distance(this, lhs);
        }
        public val_ty distance2(dvec3 lhs)
        {
            return distance2(this, lhs);
        }

        public dvec3 normalized()
        {
            return normalized(this);
        }
        public void normalize()
        {
            val_ty v = 1 / System.Math.Sqrt(x * x + y * y + z * z);

            x *= v;
            y *= v;
            z *= v;
        }

        public dvec3 abs()
        {
            return new dvec3(x < 0 ? -x : x, y < 0 ? -y : y, z < 0 ? -z : z);
        }
    }
}
