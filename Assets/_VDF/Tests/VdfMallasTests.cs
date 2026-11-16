using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VDF.Tests
{
    public class VdfMallasTests
    {
        /// <summary>
        /// Cuenta cuántas aristas de la malla pertenecen a un solo triángulo.
        ///
        /// Se comparan POSICIONES redondeadas y no índices, a propósito: las seis caras se
        /// generan por separado y no comparten vértices, así que por índice todas las
        /// aristas del borde parecerían sueltas aunque la caja esté perfectamente cerrada.
        /// Lo que importa es que los puntos coincidan en el espacio.
        /// </summary>
        private static int AristasAbiertas(Mesh malla)
        {
            var v = malla.vertices;
            var t = malla.triangles;
            var usos = new Dictionary<string, int>();

            for (int i = 0; i < t.Length; i += 3)
            {
                string[] p = { Clave(v[t[i]]), Clave(v[t[i + 1]]), Clave(v[t[i + 2]]) };
                for (int e = 0; e < 3; e++)
                {
                    string a = p[e], b = p[(e + 1) % 3];
                    string arista = string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
                    if (!usos.ContainsKey(arista)) usos[arista] = 0;
                    usos[arista]++;
                }
            }

            int abiertas = 0;
            foreach (var par in usos) if (par.Value == 1) abiertas++;
            return abiertas;
        }

        private static string Clave(Vector3 p)
        {
            // Una centésima de milímetro sobre una malla de lado 1: suficiente para que dos
            // puntos que deberían coincidir coincidan, y para que dos distintos no se mezclen.
            return Mathf.RoundToInt(p.x * 100000f) + ","
                 + Mathf.RoundToInt(p.y * 100000f) + ","
                 + Mathf.RoundToInt(p.z * 100000f);
        }

        [Test]
        public void LaCajaBiselada_EstaCerrada()
        {
            // Los tres biseles que usa el tablero: el general, el del variador y el de las
            // teclas del operador, que son casi una pastilla.
            float[] biseles = { 0.05f, 0.07f, 0.22f };

            foreach (float b in biseles)
            {
                var malla = VdfMallas.CajaBiselada(b, 2);
                Assert.AreEqual(0, AristasAbiertas(malla),
                    "con bisel " + b.ToString("0.00") + " la caja tiene agujeros: es la clase "
                    + "de fallo que se ve como piezas despegadas y que ningún otro test mira");
            }
        }

        /// <summary>
        /// Y con más segmentos tampoco se abre: el número de divisiones no puede cambiar si
        /// la pieza es sólida o no.
        /// </summary>
        [Test]
        public void LaCajaBiselada_SigueCerradaConMasSegmentos()
        {
            for (int seg = 1; seg <= 4; seg++)
            {
                var malla = VdfMallas.CajaBiselada(0.06f, seg);
                Assert.AreEqual(0, AristasAbiertas(malla),
                    "con " + seg + " segmentos la caja tiene agujeros");
            }
        }

        /// <summary>
        /// La caja ocupa su tamaño nominal: de -0,5 a +0,5 en los tres ejes.
        ///
        /// Importa porque el constructor la escala por el tamaño real de cada pieza. Si la
        /// malla midiera menos de 1, todas las piezas del tablero saldrían encogidas y las
        /// medidas reales del módulo dejarían de valer.
        /// </summary>
        [Test]
        public void LaCajaBiselada_MideLoQueDice()
        {
            var malla = VdfMallas.CajaBiselada(0.07f, 2);
            var b = malla.bounds;

            Assert.AreEqual(1f, b.size.x, 0.001f, "el ancho debería ser 1");
            Assert.AreEqual(1f, b.size.y, 0.001f, "el alto debería ser 1");
            Assert.AreEqual(1f, b.size.z, 0.001f, "el fondo debería ser 1");
            // Con tolerancia: comparar Vector3 con AreEqual exige igualdad exacta y el
            // centro sale (0,0,0) salvo el error de coma flotante de sumar 216 vertices.
            Assert.AreEqual(0f, b.center.x, 0.0005f, "debería estar centrada en X");
            Assert.AreEqual(0f, b.center.y, 0.0005f, "debería estar centrada en Y");
            Assert.AreEqual(0f, b.center.z, 0.0005f, "debería estar centrada en Z");
        }

        /// <summary>
        /// La cara tiene que ser PLANA hasta donde empieza el bisel.
        ///
        /// Es el segundo fallo de la misma malla, y salió al arreglar el primero. Con la
        /// rejilla repartida a partes iguales por toda la cara, entre el último punto
        /// interior y el borde queda un solo cuadro que va de la zona plana a la esquina
        /// redondeada: la interpolación se come la cara y la pieza sale hinchada, como una
        /// pastilla de jabón. El breaker parecía un cojín.
        ///
        /// Se comprueba que existan vértices en el centro de la cara a la profundidad
        /// exacta, que es lo que no pasaba.
        /// </summary>
        [Test]
        public void LaCajaBiselada_TieneCaraPlana()
        {
            float bisel = 0.07f;
            var malla = VdfMallas.CajaBiselada(bisel, 2);
            float limite = 0.5f - bisel;

            int planos = 0;
            foreach (var v in malla.vertices)
            {
                // Un punto de la cara −Z plana: dentro del límite en X e Y, y a −0,5 en Z.
                if (Mathf.Abs(v.z + 0.5f) < 0.0005f &&
                    Mathf.Abs(v.x) <= limite + 0.0005f &&
                    Mathf.Abs(v.y) <= limite + 0.0005f)
                {
                    planos++;
                }
            }

            Assert.GreaterOrEqual(planos, 4,
                "la cara debería tener al menos sus cuatro esquinas planas a la profundidad "
                + "nominal. Si no, el bisel se come la cara y la pieza sale hinchada");
        }
    }
}
