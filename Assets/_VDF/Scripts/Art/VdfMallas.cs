
using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Mallas propias para las piezas del tablero: cajas con los cantos matados y domos.
    ///
    /// Por qué hace falta: todo el módulo estaba construido con los primitivos de Unity, que
    /// son cubos y cilindros de arista viva. El resultado se ve cuadrado y de bloques, y es lo
    /// primero que salta al poner una captura al lado de la foto del módulo real, donde **nada
    /// tiene arista viva**: los pilotos son lentes abombadas, los pulsadores tienen la cabeza
    /// redondeada con su collar, y hasta el magnetotérmico y la carcasa del variador llevan el
    /// canto matado. Esa diferencia no se arregla con texturas ni con luz; hay que cambiar la
    /// geometría.
    ///
    /// Las mallas se cachean por sus parámetros. Un tablero tiene ochenta bornes iguales y
    /// cuatro indicadores iguales: generar una malla por objeto sería tirar memoria y romper
    /// el batching, que en Quest es lo que mantiene los fotogramas.
    /// </summary>
    public static class VdfMallas
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Dónde se guardan las mallas generadas, para que sobrevivan.</summary>
        private const string Carpeta = "Assets/_VDF/Mallas";

        private static Mesh Persistir(string clave, Mesh malla)
        {
            Cache[clave] = malla;

#if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.IsValidFolder(Carpeta))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_VDF", "Mallas");

            string ruta = Carpeta + "/" + clave + ".asset";
            var guardada = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            if (guardada != null)
            {
                Cache[clave] = guardada;
                return guardada;
            }

            UnityEditor.AssetDatabase.CreateAsset(malla, ruta);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            return malla;
        }

        /// <summary>La malla ya conocida, de la memoria o del proyecto.</summary>
        private static Mesh Conocida(string clave)
        {
            Mesh m;
            if (Cache.TryGetValue(clave, out m) && m != null) return m;

#if UNITY_EDITOR
            var guardada = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(Carpeta + "/" + clave + ".asset");
            if (guardada != null) { Cache[clave] = guardada; return guardada; }
#endif
            return null;
        }

        /// <summary>
        /// Caja con los ocho cantos matados, del tamaño 1×1×1 para poder escalarla luego.
        ///
        /// El bisel va en fracción del lado más corto, así que una caja alargada no acaba con
        /// un chaflán desproporcionado en el lado corto.
        /// </summary>
        public static Mesh CajaBiselada(float bisel = 0.06f, int segmentos = 2)
        {
            // Ojo con la clave: lleva "v2" porque la version anterior generaba una caja
            // ABIERTA y sus mallas quedaron guardadas como assets. Sin cambiar la clave se
            // seguirian reutilizando las rotas.
            string clave = "cajav3_" + bisel.ToString("0.000") + "_" + segmentos;
            var conocida = Conocida(clave);
            if (conocida != null) return conocida;

            // Cubo redondeado por PROYECCION, no por recorte de cada cara.
            //
            // La version anterior encogia cada cara hacia dentro cerca de sus bordes y no
            // generaba nada entre una cara y la siguiente: el resultado eran seis placas
            // sueltas separadas por un hueco del ancho del bisel a lo largo de las doce
            // aristas, y las ocho esquinas sencillamente no existian. En piezas pequenas el
            // hueco pasaba por una linea de sombra, pero en el variador —67 mm con bisel del
            // 5 %— son mas de 3 mm de agujero por arista y los laterales se veian como
            // tablas despegadas del cuerpo.
            //
            // Lo correcto es la suma de Minkowski de una caja con una esfera, que es lo que
            // es un canto redondeado: se toma el punto de la cara del cubo, se lleva al
            // nucleo recortandolo a la caja interior, y se empuja hacia fuera el radio del
            // bisel en la direccion que los separa.
            //
            //     nucleo  C = clamp(P, -h, +h)        con h = 0.5 - bisel
            //     punto   P' = C + bisel * normalize(P - C)
            //
            // Es estanco por construccion: dos caras vecinas comparten los mismos P en su
            // arista comun, asi que producen el mismo C, la misma normal y el mismo punto.
            // No hay que coser nada ni soldar vertices.
            var vertices = new List<Vector3>();
            var normales = new List<Vector3>();
            var triangulos = new List<int>();

            float b = Mathf.Clamp(bisel, 0.001f, 0.45f);
            float h = 0.5f - b;
            int seg = Mathf.Max(1, segmentos);

            // La rejilla NO se reparte uniformemente por la cara: se concentra en la banda
            // del bisel y deja el centro como un solo cuadro plano.
            //
            // Con los puntos repartidos a partes iguales, entre el ultimo punto interior y
            // el borde hay un unico cuadro que va de la zona plana a la esquina redondeada,
            // asi que la interpolacion se come la cara: en vez de un canto matado salia una
            // pieza hinchada, como una pastilla de jabon. El breaker parecia un cojin.
            //
            // Poniendo muestras EN el limite del bisel (0,5 - b) la cara queda plana de
            // verdad hasta ahi y el redondeo ocupa solo lo que debe.
            var muestras = new List<float>();
            for (int i = 0; i <= seg; i++) muestras.Add(-0.5f + i * b / seg);   // canto izquierdo
            for (int i = 0; i <= seg; i++) muestras.Add(h + i * b / seg);        // canto derecho
            int n = muestras.Count - 1;

            Quaternion[] giros =
            {
                Quaternion.identity,
                Quaternion.Euler(0f, 180f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                Quaternion.Euler(0f, -90f, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                Quaternion.Euler(-90f, 0f, 0f)
            };

            foreach (var giro in giros)
            {
                int baseIndice = vertices.Count;

                for (int y = 0; y < n + 1; y++)
                {
                    for (int x = 0; x < n + 1; x++)
                    {
                        float u = muestras[x];
                        float v = muestras[y];

                        Vector3 p = new Vector3(u, v, -0.5f);
                        Vector3 nucleo = new Vector3(
                            Mathf.Clamp(p.x, -h, h),
                            Mathf.Clamp(p.y, -h, h),
                            Mathf.Clamp(p.z, -h, h));

                        Vector3 fuera = p - nucleo;
                        // En el centro de la cara con bisel 0 el vector es nulo; se toma la
                        // normal de la cara para no dividir por cero.
                        Vector3 nrm = fuera.sqrMagnitude > 1e-12f
                            ? fuera.normalized
                            : new Vector3(0f, 0f, -1f);

                        vertices.Add(giro * (nucleo + nrm * b));
                        normales.Add(giro * nrm);
                    }
                }

                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        int i = baseIndice + y * (n + 1) + x;
                        triangulos.Add(i); triangulos.Add(i + n + 1); triangulos.Add(i + 1);
                        triangulos.Add(i + 1); triangulos.Add(i + n + 1); triangulos.Add(i + n + 2);
                    }
                }
            }

            var malla = new Mesh { name = "VdfCajaBiselada" };
            malla.SetVertices(vertices);
            malla.SetNormals(normales);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateBounds();

            return Persistir(clave, malla);
        }

        /// <summary>
        /// Casquete esférico: la lente de un piloto o la cabeza de un pulsador.
        ///
        /// Mide 1 de diámetro y <paramref name="altura"/> de alto, con la base en z = 0 y la
        /// cúpula hacia -Z, que es hacia donde mira la cara del tablero.
        /// </summary>
        public static Mesh Domo(float altura = 0.35f, int radiales = 20, int anillos = 5)
        {
            string clave = "domo_" + altura.ToString("0.000") + "_" + radiales + "_" + anillos;
            var conocida = Conocida(clave);
            if (conocida != null) return conocida;

            var vertices = new List<Vector3>();
            var normales = new List<Vector3>();
            var triangulos = new List<int>();

            // Radio de la esfera que pasa por el borde (r = 0.5) y por la cúspide (altura).
            float r = (0.25f + altura * altura) / (2f * altura);
            float centroZ = altura - r;

            for (int a = 0; a <= anillos; a++)
            {
                float t = a / (float)anillos;
                float radio = Mathf.Lerp(0.5f, 0f, t);
                float z = centroZ + Mathf.Sqrt(Mathf.Max(0f, r * r - radio * radio));

                for (int i = 0; i <= radiales; i++)
                {
                    float ang = i / (float)radiales * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(ang) * radio, Mathf.Sin(ang) * radio, -z);
                    vertices.Add(p);
                    normales.Add(new Vector3(p.x, p.y, -(z - centroZ)).normalized);
                }
            }

            for (int a = 0; a < anillos; a++)
            {
                for (int i = 0; i < radiales; i++)
                {
                    int f0 = a * (radiales + 1) + i;
                    int f1 = f0 + radiales + 1;

                    triangulos.Add(f0); triangulos.Add(f1); triangulos.Add(f0 + 1);
                    triangulos.Add(f0 + 1); triangulos.Add(f1); triangulos.Add(f1 + 1);
                }
            }

            var malla = new Mesh { name = "VdfDomo" };
            malla.SetVertices(vertices);
            malla.SetNormals(normales);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateBounds();

            return Persistir(clave, malla);
        }

        /// <summary>
        /// Anillo plano, para el collar metálico que rodea a pilotos y pulsadores.
        /// Diámetro exterior 1, con la cara mirando a -Z.
        /// </summary>
        public static Mesh Anillo(float interior = 0.76f, int radiales = 24)
        {
            string clave = "anillo_" + interior.ToString("0.00") + "_" + radiales;
            var conocida = Conocida(clave);
            if (conocida != null) return conocida;

            var vertices = new List<Vector3>();
            var normales = new List<Vector3>();
            var triangulos = new List<int>();

            for (int i = 0; i <= radiales; i++)
            {
                float ang = i / (float)radiales * Mathf.PI * 2f;
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                vertices.Add(new Vector3(cos * interior * 0.5f, sin * interior * 0.5f, 0f));
                vertices.Add(new Vector3(cos * 0.5f, sin * 0.5f, 0f));
                normales.Add(Vector3.back);
                normales.Add(Vector3.back);
            }

            for (int i = 0; i < radiales; i++)
            {
                int b = i * 2;
                triangulos.Add(b); triangulos.Add(b + 2); triangulos.Add(b + 1);
                triangulos.Add(b + 1); triangulos.Add(b + 2); triangulos.Add(b + 3);
            }

            var malla = new Mesh { name = "VdfAnillo" };
            malla.SetVertices(vertices);
            malla.SetNormals(normales);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateBounds();

            return Persistir(clave, malla);
        }

        /// <summary>
        /// Marco de línea fina con las esquinas redondeadas, del tamaño que se le pida.
        ///
        /// Los recuadros de la serigrafía se dibujaban con cuatro barras rectas y esquinas en
        /// pico. En el módulo real todos son rectángulos de esquina redondeada, y puestos uno
        /// al lado del otro es de lo primero que canta.
        ///
        /// No vale hacer una malla de 1×1 y escalarla: un recuadro de 420 × 70 mm convertiría
        /// la esquina en un cuarto de elipse muy estirado. La malla se genera a su medida y se
        /// cachea por parámetros, que son pocos y repetidos.
        ///
        /// El contorno exterior y el interior comparten el centro de cada esquina, así que
        /// basta recorrer el perímetro una vez y emitir un par de puntos (fuera, dentro) por
        /// paso: los tramos rectos salen solos entre el final de un arco y el principio del
        /// siguiente.
        /// </summary>
        public static Mesh MarcoRedondeado(float ancho, float alto, float radio, float grosor,
                                           int segmentosEsquina = 5)
        {
            string clave = "marco_" + ancho.ToString("0.0000") + "_" + alto.ToString("0.0000")
                         + "_" + radio.ToString("0.0000") + "_" + grosor.ToString("0.0000")
                         + "_" + segmentosEsquina;
            var conocida = Conocida(clave);
            if (conocida != null) return conocida;

            float hw = ancho * 0.5f;
            float hh = alto * 0.5f;

            // El radio no puede comerse el lado corto ni pasarse del propio grosor de línea.
            float r = Mathf.Min(radio, Mathf.Min(hw, hh));
            float ri = Mathf.Max(0f, r - grosor);
            int seg = Mathf.Max(1, segmentosEsquina);

            // Centros de las cuatro esquinas, en el orden en que se recorre el perímetro.
            var centros = new[]
            {
                new Vector2(hw - r, hh - r),      // arriba derecha
                new Vector2(-(hw - r), hh - r),   // arriba izquierda
                new Vector2(-(hw - r), -(hh - r)),// abajo izquierda
                new Vector2(hw - r, -(hh - r))    // abajo derecha
            };

            var fuera = new List<Vector3>();
            var dentro = new List<Vector3>();

            for (int c = 0; c < 4; c++)
            {
                float desde = c * 90f;
                for (int i = 0; i <= seg; i++)
                {
                    float ang = (desde + 90f * i / seg) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    fuera.Add(new Vector3(centros[c].x + dir.x * r, centros[c].y + dir.y * r, 0f));
                    dentro.Add(new Vector3(centros[c].x + dir.x * ri, centros[c].y + dir.y * ri, 0f));
                }
            }

            var vertices = new List<Vector3>();
            var normales = new List<Vector3>();
            var triangulos = new List<int>();

            int n = fuera.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int b = vertices.Count;

                vertices.Add(fuera[i]); vertices.Add(dentro[i]);
                vertices.Add(fuera[j]); vertices.Add(dentro[j]);
                for (int k = 0; k < 4; k++) normales.Add(Vector3.back);

                // Antihorario visto desde -Z, que es de donde se mira el tablero.
                triangulos.Add(b); triangulos.Add(b + 1); triangulos.Add(b + 2);
                triangulos.Add(b + 2); triangulos.Add(b + 1); triangulos.Add(b + 3);
            }

            var malla = new Mesh { name = "VdfMarcoRedondeado" };
            malla.SetVertices(vertices);
            malla.SetNormals(normales);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateBounds();

            return Persistir(clave, malla);
        }

        /// <summary>
        /// Crea un objeto con una de estas mallas. Sin collider: el del componente lo pone
        /// quien lo necesita, y la mayoría de estas piezas son decorado.
        /// </summary>
        public static GameObject Pieza(Transform padre, string nombre, Mesh malla,
                                       Vector3 posicion, Vector3 escala)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posicion;
            go.transform.localScale = escala;

            go.AddComponent<MeshFilter>().sharedMesh = malla;
            go.AddComponent<MeshRenderer>();
            return go;
        }
    }
}
