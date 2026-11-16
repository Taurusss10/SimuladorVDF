// NOTA: un MonoBehaviour por archivo. Ver E6 en VIABILIDAD.md.

using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Cable banana con física propia: integración de Verlet con restricciones de distancia
    /// y colisión contra primitivas analíticas.
    ///
    /// **Por qué no se usa el motor de física de Unity.** Un cable con rigidbodies serían
    /// ~8 cuerpos articulados; con 20 cables, 160 cuerpos y 160 joints. Resolver joints es
    /// lo caro de PhysX en móvil, y a 72 Hz eso se come una parte seria del presupuesto de
    /// 13,9 ms por frame en una Quest.
    ///
    /// Aquí cada cable son 14 masas puntuales sin rigidbody, con 6 iteraciones de
    /// restricción. El coste es despreciable y, sobre todo, **agarrar un extremo es
    /// simplemente fijar un punto**: no hay joints kinemáticos ni latigazos.
    ///
    /// La colisión es contra un puñado de formas analíticas que se registran a mano
    /// (el plano del tablero, la caja del variador, el cilindro del motor, la mesa), no
    /// contra la malla de la escena. Es lo que hace falta para que el cable **se apoye y no
    /// atraviese nada**, que es lo que se ve; el cable contra cable se deja fuera a
    /// propósito porque es lo más caro y lo que menos aporta.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class VdfCablePhysics : MonoBehaviour
    {
        [Header("Simulación")]
        [Tooltip("Número de masas puntuales. 14 da una caída suave sin coste apreciable.")]
        public int Nodos = 14;

        [Tooltip("Longitud del cable en metros. Si sobra, el cable cuelga; si falta, tira.")]
        public float Longitud = 0.55f;

        [Tooltip("Iteraciones del solucionador de restricciones por paso.")]
        [Range(1, 16)] public int Iteraciones = 10;

        [Tooltip("Amortiguación: 0 = sin rozamiento, 1 = se para en seco.")]
        [Range(0f, 1f)] public float Amortiguacion = 0.22f;

        /// <summary>
        /// Por debajo de este movimiento por paso, el cable se da por quieto y se deja de
        /// simular hasta que alguien lo toque.
        ///
        /// Sin esto el cable nunca se para del todo: el solucionador y la gravedad se pelean
        /// por décimas de milímetro y el resultado es un temblor permanente que en el vídeo
        /// se ve como si los cables «se movieran solos». Tampoco es solo estético: veinte
        /// cables simulando a 50 Hz para siempre es trabajo tirado, y en Quest se nota.
        /// </summary>
        public float UmbralDeReposo = 0.00035f;

        /// <summary>Cuántos pasos seguidos quieto hacen falta para dormirlo.</summary>
        public int PasosParaDormir = 12;

        private int _quietos;
        private bool _dormido;

        public float Gravedad = -9.81f;

        [Header("Aspecto")]
        public float Grosor = 0.0045f;

        [Tooltip("Veces que se resuelve el contacto entre cables en cada paso.")]
        public int IteracionesSeparacion = 3;

        [Tooltip("Fraccion del solape que se corrige en cada iteracion. Bajo = estable.")]
        [Range(0.05f, 1f)]
        public float RelajacionSeparacion = 0.30f;
        public Color Color = Color.red;

        [Header("Identidad")]
        // Desde la fase 4 el cable con física YA NO es solo un adorno: es el cable de verdad,
        // el que el grafo de cableado conoce. Para eso tiene que saber qué dos bornes une, y
        // tiene que poder seleccionarse con el ratón o con el rayo del mando.
        [Tooltip("Id del borne del extremo A, tal como lo conoce VdfWiringGraph.")]
        public string IdA;
        [Tooltip("Id del borne del extremo B.")]
        public string IdB;

        [Header("Selección")]
        [Tooltip("Cápsulas de colisión repartidas a lo largo de la curva. Con 1 sola, el " +
                 "volumen de selección es la cuerda recta y hay que apuntar por encima de " +
                 "donde se ve el cable colgado.")]
        [Range(1, 8)] public int SegmentosCollider = 4;

        [Tooltip("Margen sobre el grosor del cable, para que acertarle no exija puntería.")]
        public float HolguraCollider = 0.004f;

        [Header("Salida del borne")]
        /// <summary>
        /// Cuánto sale el cable en perpendicular al tablero antes de empezar a caer.
        ///
        /// Sin esto, el cable nace pegado a la chapa y, al tensarse entre dos bornes, describe
        /// un arco que pasa POR DELANTE de los pulsadores y de los pilotos: se veían cables
        /// cruzando el botón de SUBIR por el medio, que es justo lo que en un tablero real no
        /// pasa nunca. En el módulo de verdad cada cable sale recto del borne, baja por
        /// gravedad y vuelve a subir al otro, sin tocar la cara del tablero.
        /// </summary>
        public float SalidaPerpendicular = 0.018f;

        // --- Extremos ----------------------------------------------------------------------
        /// <summary>Punto al que está fijado el extremo A, o null si está suelto.</summary>
        public Transform AnclaA;
        public Transform AnclaB;

        private Vector3[] _pos;
        private Vector3[] _prev;
        private bool[] _fijo;
        private LineRenderer _linea;
        private float _separacion;

        [SerializeField] private CapsuleCollider[] _capsulas;

        /// <summary>¿Este cable llega a ese borne?</summary>
        public bool Toca(string id)
        {
            return !string.IsNullOrEmpty(id) && (IdA == id || IdB == id);
        }

        /// <summary>El otro extremo del cable, o null si el borne no es ninguno de los dos.</summary>
        public string Otro(string id)
        {
            if (IdA == id) return IdB;
            if (IdB == id) return IdA;
            return null;
        }

        /// <summary>¿Une exactamente estos dos bornes, en cualquier orden?</summary>
        public bool Une(string idA, string idB)
        {
            return (IdA == idA && IdB == idB) || (IdA == idB && IdB == idA);
        }

        /// <summary>
        /// Aclara el cable al pasar el cursor por encima.
        ///
        /// Se cambia el MATERIAL, no startColor/endColor: el shader no multiplica por color
        /// de vértice, así que tintar el LineRenderer no pinta nada. Los dos materiales
        /// salen de la caché de <see cref="VdfCableSpawner"/>, de modo que resaltar no crea
        /// materiales nuevos ni rompe el batching.
        /// </summary>
        public void Resaltar(bool activo)
        {
            if (_linea == null) _linea = GetComponent<LineRenderer>();
            if (_linea == null) return;

            Color c = activo ? Color.Lerp(Color, UnityEngine.Color.white, 0.55f) : Color;
            var mat = VdfCableSpawner.MaterialDe(c);
            _linea.sharedMaterial = mat;

            // El que se ve es el tubo: la cinta esta apagada desde que se dibuja en 3D.
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        /// <summary>Formas contra las que choca el cable. Compartidas por todos los cables.</summary>
        public static readonly List<VdfColisionadorCable> Colisionadores = new List<VdfColisionadorCable>();

        /// <summary>
        /// Todos los cables que hay ahora mismo en la escena.
        ///
        /// Hace falta para que un cable pueda apartarse de los demas. Se mantiene a mano, con
        /// OnEnable y OnDisable, en vez de buscarlos con FindObjectsByType cada fotograma:
        /// esa busqueda recorre la escena entera y aqui se llama en FixedUpdate.
        /// </summary>
        public static readonly List<VdfCablePhysics> Vivos = new List<VdfCablePhysics>();

        private void OnEnable()
        {
            Registrar();
        }

        /// <summary>
        /// Se apunta en la lista de cables vivos.
        ///
        /// Se llama desde OnEnable y TAMBIEN desde Inicializar, y eso segundo no es
        /// redundante: OnEnable solo corre al entrar en Play, pero los cables se tienden en
        /// el EDITOR —VDF > Reconstruir los crea y los asienta ahi mismo—, asi que durante
        /// todo ese asentado la lista estaba vacia y la separacion entre cables no se
        /// ejecutaba ni una vez. Los cables se asentaban atravesandose y asi quedaban
        /// guardados en la escena.
        ///
        /// Se podria resolver con [ExecuteAlways], pero eso haria correr tambien FixedUpdate
        /// en el editor y los cables estarian simulando siempre, ensuciando la escena.
        /// </summary>
        private void Registrar()
        {
            // De paso se tiran los que ya no existen: sin OnDisable en el editor, la lista
            // acumula referencias muertas de cada reconstruccion.
            for (int i = Vivos.Count - 1; i >= 0; i--)
                if (Vivos[i] == null) Vivos.RemoveAt(i);

            if (!Vivos.Contains(this)) Vivos.Add(this);
        }

        private void OnDisable()
        {
            Vivos.Remove(this);
        }

        // =====================================================================================
        // Ciclo de vida
        // =====================================================================================

        public void Inicializar(Transform anclaA, Transform anclaB, Color color, Material material)
        {
            AnclaA = anclaA;
            AnclaB = anclaB;
            Color = color;

            _linea = GetComponent<LineRenderer>();
            _linea.useWorldSpace = true;
            _linea.positionCount = Nodos;
            _linea.startWidth = Grosor;
            _linea.endWidth = Grosor;
            _linea.numCapVertices = 4;
            _linea.numCornerVertices = 3;
            _linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _linea.receiveShadows = false;
            if (material != null) _linea.sharedMaterial = material;

            Registrar();
            ConstruirCapsulas();

            // Reiniciar PRIMERO y el tubo despues.
            //
            // Reiniciar es quien fija Nodos —lo sube a 4 como minimo— y quien reserva los
            // arreglos de posiciones. Preparando el tubo antes, sus anillos se dimensionaban
            // con el Nodos viejo y quedaban descuadrados respecto a _pos: en cuanto los dos
            // numeros no coincidian, cualquier recorrido por nodos se salia del arreglo.
            Reiniciar();
            PrepararTubo(material);
        }

        // =====================================================================================
        // Volumen de selección
        // =====================================================================================

        /// <summary>
        /// Crea las cápsulas que siguen la curva.
        ///
        /// Cuelgan de hijos propios y no del cable, porque una cápsula tiene que orientarse
        /// a lo largo de su tramo y el objeto del cable no puede rotar (sus nodos están en
        /// coordenadas de mundo).
        ///
        /// Son triggers: no deben empujar nada ni participar en la física: solo existen para
        /// que el rayo del ratón y el del mando puedan acertarle al cable.
        /// </summary>
        private void ConstruirCapsulas()
        {
            int n = Mathf.Clamp(SegmentosCollider, 1, 8);

            if (_capsulas != null && _capsulas.Length == n)
            {
                bool completas = true;
                for (int i = 0; i < _capsulas.Length; i++)
                    if (_capsulas[i] == null) { completas = false; break; }
                if (completas) return;
            }

            if (_capsulas != null)
            {
                for (int i = 0; i < _capsulas.Length; i++)
                    if (_capsulas[i] != null) DestruirSegun(_capsulas[i].gameObject);
            }

            _capsulas = new CapsuleCollider[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Tramo_" + i);
                go.transform.SetParent(transform, false);

                var c = go.AddComponent<CapsuleCollider>();
                c.direction = 1;                 // eje Y local: el tramo apunta hacia arriba
                c.isTrigger = true;
                c.radius = Grosor * 0.5f + HolguraCollider;
                _capsulas[i] = c;
            }
        }

        /// <summary>Coloca cada cápsula sobre su tramo de la curva.</summary>
        private void ActualizarCapsulas()
        {
            if (_capsulas == null || _capsulas.Length == 0 || _pos == null) return;

            int tramos = _capsulas.Length;
            for (int i = 0; i < tramos; i++)
            {
                var c = _capsulas[i];
                if (c == null) continue;

                int desde = Mathf.RoundToInt((float)i / tramos * (Nodos - 1));
                int hasta = Mathf.RoundToInt((float)(i + 1) / tramos * (Nodos - 1));
                if (hasta <= desde) hasta = Mathf.Min(Nodos - 1, desde + 1);

                Vector3 a = _pos[desde];
                Vector3 b = _pos[hasta];
                Vector3 eje = b - a;
                float largo = eje.magnitude;

                var t = c.transform;
                t.position = (a + b) * 0.5f;
                if (largo > 1e-5f) t.rotation = Quaternion.FromToRotation(Vector3.up, eje / largo);

                // La altura de una cápsula incluye sus dos semiesferas, así que el tramo
                // recto es height - 2*radius. Sumarlo evita que los tramos dejen huecos.
                c.height = largo + c.radius * 2f;
            }
        }

        private void DestruirSegun(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>Coloca los nodos en línea recta entre los dos anclajes y para el movimiento.</summary>
        public void Reiniciar()
        {
            Nodos = Mathf.Max(4, Nodos);
            _pos = new Vector3[Nodos];
            _prev = new Vector3[Nodos];
            _fijo = new bool[Nodos];
            _separacion = Longitud / (Nodos - 1);

            Vector3 a = AnclaA != null ? AnclaA.position : transform.position;
            Vector3 b = AnclaB != null ? AnclaB.position : transform.position + Vector3.right * Longitud;

            // Los dos puntos por los que el cable sale de su borne, antes de caer.
            Vector3 salidaA = AnclaA != null
                ? a + AnclaA.TransformDirection(Vector3.back).normalized * SalidaPerpendicular : a;
            Vector3 salidaB = AnclaB != null
                ? b + AnclaB.TransformDirection(Vector3.back).normalized * SalidaPerpendicular : b;

            for (int i = 0; i < Nodos; i++)
            {
                float t = (float)i / (Nodos - 1);

                // Sale recto del borne, cuelga por el medio y vuelve a entrar recto en el otro.
                Vector3 baseP = Vector3.Lerp(salidaA, salidaB, t);
                if (i == 0) baseP = a;
                else if (i == Nodos - 1) baseP = b;

                _pos[i] = baseP;
                _pos[i].y -= Mathf.Sin(t * Mathf.PI) * Longitud * 0.22f;

                // Un empujón lateral minúsculo, distinto para cada cable y para cada tramo.
                // Sin él, veinte cables con el mismo trazado inicial caen exactamente igual y
                // acaban formando un peine; en un tablero real no hay dos que cuelguen igual.
                // La semilla sale de la posición del anclaje, así que el desorden es siempre
                // el mismo: la escena no cambia de aspecto cada vez que se reconstruye.
                float semilla = (a.x + a.y * 7.31f + b.x * 3.17f) * 100f;
                float ruido = Mathf.Sin(semilla + t * 9.1f) * 0.5f + Mathf.Sin(semilla * 2.7f + t * 4.3f) * 0.5f;
                _pos[i] += new Vector3(ruido * Longitud * 0.035f, 0f,
                                       Mathf.Cos(semilla + t * 6.7f) * Longitud * 0.045f)
                           * Mathf.Sin(t * Mathf.PI);

                _prev[i] = _pos[i];
            }

            _dormido = false;
            _quietos = 0;

            if (_linea != null) VolcarALinea();
            ActualizarCapsulas();
        }

        /// <summary>
        /// Deja caer el cable hasta que se queda quieto, sin necesidad de estar en Play.
        ///
        /// Es lo que hace que los cables dejen de verse tiesos. El trazado inicial es una
        /// interpolación con una panza senoidal: una curva perfecta, calculada, **idéntica en
        /// todos los cables**, y el ojo la reconoce enseguida como algo dibujado con plantilla.
        /// Un cable de verdad cuelga en catenaria, se apoya en lo que encuentra y no hay dos
        /// iguales. Eso solo sale de dejar correr la simulación.
        ///
        /// En la escena guardada no hay simulación ninguna, así que hay que correrla aquí y
        /// guardar el resultado: los cables quedan ya caídos antes de darle a Play.
        /// </summary>
        /// <summary>
        /// Asienta TODOS los cables a la vez.
        ///
        /// Hace falta porque los cables se tienden de uno en uno y cada uno se asienta nada
        /// mas nacer: el primero cae solo, el segundo se acomoda contra el primero, el
        /// tercero contra los dos... y los primeros nunca vuelven a relajarse. El resultado
        /// es que la separacion entre cables, que si se ejecuta, se aplica contra un enredo
        /// ya congelado y no lo deshace.
        ///
        /// Avanzando los diecinueve en paralelo, cada paso ve a los demas en su sitio actual
        /// y el mazo se ordena: los cables acaban apoyados UNOS SOBRE OTROS, que es como
        /// quedan en una mesa de verdad, en vez de atravesandose.
        /// </summary>
        public static void AsentarTodos(int pasos = 240, float dt = 0.02f)
        {
            for (int i = Vivos.Count - 1; i >= 0; i--)
                if (Vivos[i] == null) Vivos.RemoveAt(i);

            for (int p = 0; p < pasos; p++)
                for (int i = 0; i < Vivos.Count; i++)
                {
                    var c = Vivos[i];
                    if (c == null) continue;
                    c._dormido = false;
                    c.Paso(dt);
                }

            for (int i = 0; i < Vivos.Count; i++)
                if (Vivos[i] != null) { Vivos[i]._dormido = true; Vivos[i]._quietos = 0; }
        }

        public void Asentar(int pasos = 420, float dt = 0.02f)
        {
            if (_pos == null || _pos.Length < 2) Reiniciar();

            for (int i = 0; i < pasos; i++) Paso(dt);

            _dormido = true;
            _quietos = 0;
        }

        private void Paso(float dt)
        {
            FijarExtremos();
            Integrar(dt);

            for (int it = 0; it < Iteraciones; it++)
            {
                AplicarRestricciones();
                FijarExtremos();
            }

            LimitarEstiramiento();
            Colisionar();
            LimitarEstiramiento();

            // La separacion entre cables va LA ULTIMA, despues de limitar el estiramiento, y
            // se repite varias veces.
            //
            // Lo de ir la ultima es porque el limitador tiraba de los nodos hacia dentro y
            // deshacia el empuje en el mismo paso. Y lo de repetirla es porque con
            // diecinueve cables en un mazo, apartar A de B lo mete contra C: cada pasada
            // arregla unos pares y estropea otros, y hace falta insistir para que el
            // conjunto se asiente. Es el mismo motivo por el que las restricciones de la
            // cuerda tambien se resuelven en varias iteraciones.
            for (int it = 0; it < IteracionesSeparacion; it++) SepararDeOtrosCables();

            VolcarALinea();
            ActualizarCapsulas();
        }

        private void Start()
        {
            if (_pos == null || _pos.Length != Mathf.Max(4, Nodos)) Rehacer();

            _dormido = false;
            _fotogramasDeGracia = 30;
        }

        /// <summary>Vuelve a montar el cuerpo del cable con lo que quedo serializado.</summary>
        private void Rehacer()
        {
            var r = GetComponent<MeshRenderer>();
            Material material = r != null ? r.sharedMaterial : null;
            if (material == null && _linea != null) material = _linea.sharedMaterial;

            Registrar();
            ConstruirCapsulas();
            Reiniciar();
            PrepararTubo(material);
        }

        private int _fotogramasDeGracia;

        private void FixedUpdate()
        {
            if (_pos == null || _pos.Length < 2) return;

            if (_fotogramasDeGracia > 0)
            {
                _fotogramasDeGracia--;
                _dormido = false;
            }

            if (_dormido && !SeMovioUnExtremo()) return;

            Vector3[] antes = _dormido ? null : Copia(_pos);
            _dormido = false;

            Paso(Time.fixedDeltaTime);

            if (antes != null) RevisarSiSeDuerme(antes);
        }

        /// <summary>¿Se ha movido lo bastante poco como para darlo por quieto?</summary>
        private void RevisarSiSeDuerme(Vector3[] antes)
        {
            float maximo = 0f;
            for (int i = 0; i < _pos.Length && i < antes.Length; i++)
            {
                float d = (_pos[i] - antes[i]).sqrMagnitude;
                if (d > maximo) maximo = d;
            }

            if (maximo > UmbralDeReposo * UmbralDeReposo) { _quietos = 0; return; }

            _quietos++;
            if (_quietos < PasosParaDormir) return;

            // Al dormirlo se igualan las posiciones previas: si no, al despertar arrastraría
            // la velocidad que tuviera guardada y pegaría un salto.
            for (int i = 0; i < _pos.Length; i++) _prev[i] = _pos[i];
            _dormido = true;
            _quietos = 0;
        }

        /// <summary>Un cable dormido despierta si le mueven un extremo (lo enchufan o lo sacan).</summary>
        private bool SeMovioUnExtremo()
        {
            if (AnclaA != null && (AnclaA.position - _pos[0]).sqrMagnitude > 1e-8f) return true;
            if (AnclaB != null && (AnclaB.position - _pos[_pos.Length - 1]).sqrMagnitude > 1e-8f) return true;
            return false;
        }

        private static Vector3[] Copia(Vector3[] v)
        {
            var c = new Vector3[v.Length];
            System.Array.Copy(v, c, v.Length);
            return c;
        }

        // =====================================================================================
        // Solucionador
        // =====================================================================================

        private void FijarExtremos()
        {
            for (int i = 0; i < _fijo.Length; i++) _fijo[i] = false;

            if (AnclaA != null)
            {
                _pos[0] = AnclaA.position; _prev[0] = _pos[0]; _fijo[0] = true;
                Fijar(1, AnclaA);
            }
            if (AnclaB != null)
            {
                int u = Nodos - 1;
                _pos[u] = AnclaB.position; _prev[u] = _pos[u]; _fijo[u] = true;
                Fijar(u - 1, AnclaB);
            }
        }

        /// <summary>
        /// Clava el segundo nodo delante de su borne, para que el cable salga perpendicular.
        ///
        /// La dirección se toma del propio anclaje y no del eje Z del mundo: el PLC está girado
        /// 22° respecto al tablero, y con una dirección fija sus cables salían de lado.
        /// </summary>
        private void Fijar(int indice, Transform ancla)
        {
            if (indice <= 0 || indice >= Nodos - 1) return;
            if (SalidaPerpendicular <= 0f) return;

            Vector3 fuera = ancla.TransformDirection(Vector3.back).normalized;
            _pos[indice] = ancla.position + fuera * SalidaPerpendicular;
            _prev[indice] = _pos[indice];
            _fijo[indice] = true;
        }

        /// <summary>Verlet: la velocidad está implícita en la diferencia con la posición anterior.</summary>
        private void Integrar(float dt)
        {
            Vector3 g = new Vector3(0f, Gravedad, 0f) * dt * dt;
            float retencion = 1f - Mathf.Clamp01(Amortiguacion);

            for (int i = 0; i < Nodos; i++)
            {
                if (_fijo[i]) continue;

                Vector3 actual = _pos[i];
                Vector3 velocidad = (actual - _prev[i]) * retencion;
                _pos[i] = actual + velocidad + g;
                _prev[i] = actual;
            }
        }

        /// <summary>Mantiene la distancia entre nodos vecinos. Es lo que da el comportamiento de cuerda.</summary>
        private void AplicarRestricciones()
        {
            for (int i = 0; i < Nodos - 1; i++)
            {
                Vector3 delta = _pos[i + 1] - _pos[i];
                float d = delta.magnitude;
                if (d < 1e-6f) continue;

                float error = (d - _separacion) / d;
                Vector3 correccion = delta * error * 0.5f;

                bool fa = _fijo[i], fb = _fijo[i + 1];
                if (fa && fb) continue;

                if (fa) { _pos[i + 1] -= correccion * 2f; }
                else if (fb) { _pos[i] += correccion * 2f; }
                else { _pos[i] += correccion; _pos[i + 1] -= correccion; }
            }
        }

        /// <summary>
        /// Recorre la cadena desde los extremos fijos hacia dentro y recorta cualquier
        /// segmento que se haya pasado de largo.
        ///
        /// Gauss-Seidel converge despacio en cadenas largas: la correccion solo se propaga
        /// un nodo por iteracion, asi que con 6 iteraciones y 24 nodos el centro del cable
        /// casi no se entera y el cable se estira como un chicle. Esta pasada es dura
        /// (fija la distancia, no la relaja) y cuesta O(n), que es justo lo que hace falta
        /// para que un cable parezca inextensible sin subir las iteraciones a 50.
        /// </summary>
        private void LimitarEstiramiento()
        {
            float maximo = _separacion * 1.02f;

            // Desde el extremo A, si esta anclado.
            if (AnclaA != null)
            {
                for (int i = 0; i < Nodos - 1; i++)
                {
                    if (_fijo[i + 1]) continue;
                    Vector3 delta = _pos[i + 1] - _pos[i];
                    float d = delta.magnitude;
                    if (d > maximo && d > 1e-6f) _pos[i + 1] = _pos[i] + delta * (maximo / d);
                }
            }

            // Y desde el extremo B.
            if (AnclaB != null)
            {
                for (int i = Nodos - 1; i > 0; i--)
                {
                    if (_fijo[i - 1]) continue;
                    Vector3 delta = _pos[i - 1] - _pos[i];
                    float d = delta.magnitude;
                    if (d > maximo && d > 1e-6f) _pos[i - 1] = _pos[i] + delta * (maximo / d);
                }
            }
        }

        /// <summary>Empuja los nodos fuera de las formas registradas.</summary>
        private void Colisionar()
        {
            float radio = Grosor * 0.5f;

            for (int i = 0; i < Nodos; i++)
            {
                if (_fijo[i]) continue;

                for (int c = 0; c < Colisionadores.Count; c++)
                {
                    var col = Colisionadores[c];
                    if (col == null) continue;

                    Vector3 corregido;
                    if (col.Empujar(_pos[i], radio, out corregido))
                    {
                        // Rozamiento al apoyarse: se frena el deslizamiento lateral.
                        Vector3 velocidad = (_pos[i] - _prev[i]) * 0.45f;
                        _pos[i] = corregido;
                        _prev[i] = corregido - velocidad;
                    }
                }
            }
        }

        /// <summary>
        /// Aparta este cable de los demas, comparando SEGMENTOS y no nodos.
        ///
        /// Hasta ahora los cables solo chocaban contra el tablero y el banco, nunca entre
        /// ellos: con veinte colgando del mismo sitio se metian unos dentro de otros y el
        /// mazo se veia como una sola masa de colores.
        ///
        /// El primer intento comparaba NODO contra NODO y no bastaba, por geometria: los
        /// nodos van cada 2 cm y el cable mide 4,5 mm de grueso, asi que dos cables pueden
        /// cruzarse limpiamente ENTRE nodos sin que ningun par de nodos llegue a estar a 4,5
        /// mm. Y eso es justo lo que se veia: cables atravesandose por el medio.
        ///
        /// Lo correcto es la distancia entre los dos segmentos. Para que no cueste, cada par
        /// se descarta antes con una prueba barata: si los centros de los dos segmentos
        /// estan mas lejos que la suma de sus medias longitudes mas el margen, no pueden
        /// tocarse. Con eso la inmensa mayoria de los pares se va en tres restas.
        ///
        /// El empuje se reparte entre los dos extremos de cada segmento segun donde caiga el
        /// punto mas cercano, que es lo que evita que aparezcan picos.
        /// </summary>
        private void SepararDeOtrosCables()
        {
            if (Vivos.Count < 2 || _pos == null) return;

            int mio = Vivos.IndexOf(this);
            if (mio < 0) return;

            float radio = Grosor * 0.5f;

            for (int c = mio + 1; c < Vivos.Count; c++)
            {
                var otro = Vivos[c];
                if (otro == null || otro._pos == null || otro._pos.Length < 2) continue;

                float minimo = radio + otro.Grosor * 0.5f;

                for (int i = 0; i + 1 < Nodos; i++)
                {
                    Vector3 a0 = _pos[i], a1 = _pos[i + 1];
                    Vector3 centroA = (a0 + a1) * 0.5f;
                    float medioA = Vector3.Distance(a0, a1) * 0.5f;

                    for (int j = 0; j + 1 < otro.Nodos; j++)
                    {
                        Vector3 b0 = otro._pos[j], b1 = otro._pos[j + 1];
                        Vector3 centroB = (b0 + b1) * 0.5f;
                        float medioB = Vector3.Distance(b0, b1) * 0.5f;

                        // Descarte barato por centros.
                        float alcance = medioA + medioB + minimo;
                        if ((centroA - centroB).sqrMagnitude > alcance * alcance) continue;

                        float s, t;
                        Vector3 pa, pb;
                        CercaníaSegmentos(a0, a1, b0, b1, out s, out t, out pa, out pb);

                        Vector3 d = pa - pb;
                        float d2 = d.sqrMagnitude;
                        if (d2 >= minimo * minimo || d2 < 1e-12f) continue;

                        float dist = Mathf.Sqrt(d2);
                        Vector3 dir = d / dist;

                        // El empuje va AMORTIGUADO, no corrige todo el solape de golpe.
                        //
                        // Se probo a corregirlo entero, y con un sesgo hacia arriba para que
                        // al cruzarse uno montara sobre el otro. Las dos cosas hacian el
                        // sistema inestable: con diecinueve cables atados por los dos
                        // extremos y de longitud fija, apartar A de B lo mete contra C, y el
                        // conjunto no llegaba a un reposo. Medido: tras 300 pasos de
                        // asentado los nodos seguian moviendose 5 mm, que es lo que se veia
                        // como un parpadeo entre cables. Y ademas nunca se dormian, asi que
                        // en el visor estarian calculando para siempre.
                        //
                        // Corrigiendo una fraccion por iteracion el sistema si converge: los
                        // cables se apartan lo que pueden, se quedan quietos y se duermen.
                        // Queda algun roce entre cables que se cruzan, que a la distancia a
                        // la que se mira el mazo no se distingue, y a cambio no hay vibracion.
                        Vector3 empuje = dir * ((minimo - dist) * 0.5f * RelajacionSeparacion);

                        // Se reparte entre los extremos segun donde cayo el punto cercano.
                        Mover(this, i, empuje * (1f - s));
                        Mover(this, i + 1, empuje * s);
                        Mover(otro, j, -empuje * (1f - t));
                        Mover(otro, j + 1, -empuje * t);

                        otro._dormido = false;
                    }
                }
            }
        }

        /// <summary>
        /// Mueve un nodo si no esta enchufado a un borne.
        ///
        /// Mueve TAMBIEN la posicion anterior, y eso no es un detalle. Con integracion de
        /// Verlet la velocidad es la diferencia entre la posicion actual y la anterior, asi
        /// que empujar solo la actual equivale a darle un empujon de velocidad al cable. Con
        /// la separacion corriendo cada paso y cuatro veces por paso, eso inyecta energia sin
        /// parar y los cables se ponen a vibrar unos contra otros: se veian parpadeando.
        ///
        /// Desplazando las dos a la vez, la correccion es puramente de posicion y el cable
        /// se queda quieto donde se le deja.
        /// </summary>
        private static void Mover(VdfCablePhysics cable, int nodo, Vector3 delta)
        {
            if (nodo < 0 || nodo >= cable._pos.Length) return;
            if (cable._fijo[nodo]) return;

            cable._pos[nodo] += delta;
            cable._prev[nodo] += delta;
        }

        /// <summary>
        /// Puntos mas cercanos entre dos segmentos, con sus parametros 0..1.
        /// Es el algoritmo clasico de Ericson, con los casos degenerados resueltos.
        /// </summary>
        private static void CercaníaSegmentos(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2,
                                              out float s, out float t,
                                              out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);

            const float eps = 1e-10f;

            if (a <= eps && e <= eps) { s = t = 0f; c1 = p1; c2 = p2; return; }

            if (a <= eps)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float cc = Vector3.Dot(d1, r);
                if (e <= eps)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-cc / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;

                    s = denom > eps ? Mathf.Clamp01((b * f - cc * e) / denom) : 0f;
                    t = (b * s + f) / e;

                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-cc / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - cc) / a); }
                }
            }

            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }

        // --- El tubo ----------------------------------------------------------------------
        //
        // El cable se dibuja como un TUBO de verdad, no con el LineRenderer.
        //
        // Un LineRenderer es una cinta que siempre se gira hacia la camara: de lejos cuela,
        // pero en primer plano se ve lo que es, una banda plana, y no recibe la luz como un
        // cilindro.
        //
        // Dos detalles que importan mas de lo que parece, los dos salidos de mirar el video
        // con lupa:
        //
        //   - OCHO lados y no seis. Con seis, de cerca el cable es un prisma hexagonal y se
        //     le ven las aristas a lo largo.
        //   - La linea central se SUAVIZA con Catmull-Rom antes de generar los anillos. Los
        //     nodos de la fisica van cada 2 cm, asi que uniendolos a secas el cable es una
        //     cadena de tramos rectos con picos en cada nodo: se veian los quiebros sobre la
        //     mesa, sobre todo en los cables largos. Interpolando entre nodos la curva queda
        //     continua sin tocar la fisica, que sigue trabajando con los nodos de siempre.
        private const int LadosTubo = 8;
        private const int SubdivTubo = 3;

        private Mesh _tubo;
        private Vector3[] _vTubo;
        private Vector3[] _nTubo;
        private MeshFilter _filtroTubo;
        private int _anillosTubo;

        private void PrepararTubo(Material material)
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (material != null) mr.sharedMaterial = material;

            _filtroTubo = GetComponent<MeshFilter>();
            if (_filtroTubo == null) _filtroTubo = gameObject.AddComponent<MeshFilter>();

            _tubo = new Mesh { name = "CableTubo" };
            _tubo.MarkDynamic();

            _anillosTubo = (Nodos - 1) * SubdivTubo + 1;
            _vTubo = new Vector3[_anillosTubo * LadosTubo];
            _nTubo = new Vector3[_anillosTubo * LadosTubo];

            var tris = new int[(_anillosTubo - 1) * LadosTubo * 6];
            int k = 0;
            for (int a = 0; a < _anillosTubo - 1; a++)
            {
                for (int l = 0; l < LadosTubo; l++)
                {
                    int l2 = (l + 1) % LadosTubo;
                    int i0 = a * LadosTubo + l;
                    int i1 = a * LadosTubo + l2;
                    int i2 = (a + 1) * LadosTubo + l;
                    int i3 = (a + 1) * LadosTubo + l2;
                    tris[k++] = i0; tris[k++] = i2; tris[k++] = i1;
                    tris[k++] = i1; tris[k++] = i2; tris[k++] = i3;
                }
            }

            _tubo.vertices = _vTubo;
            _tubo.triangles = tris;
            _filtroTubo.sharedMesh = _tubo;

            if (_linea != null) _linea.enabled = false;
        }

        /// <summary>Nodo con el indice recortado a los extremos.</summary>
        private Vector3 NodoSeguro(int i)
        {
            if (_pos == null || _pos.Length == 0) return Vector3.zero;
            return _pos[Mathf.Clamp(i, 0, _pos.Length - 1)];
        }

        /// <summary>Catmull-Rom: pasa por p1 y p2 y empalma suave con los vecinos.</summary>
        private static Vector3 Suave(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * u
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        private void VolcarALinea()
        {
            if (_tubo == null || _vTubo == null || _pos == null) return;

            float radio = Grosor * 0.5f;
            Vector3 arriba = Vector3.up;

            for (int a = 0; a < _anillosTubo; a++)
            {
                float t = a / (float)SubdivTubo;
                int i = Mathf.Min((int)t, Nodos - 2);
                float u = t - i;

                Vector3 centro = Suave(NodoSeguro(i - 1), NodoSeguro(i),
                                       NodoSeguro(i + 1), NodoSeguro(i + 2), u);

                // La tangente, por diferencia con el anillo siguiente de la misma curva.
                float t2 = (a + 1) / (float)SubdivTubo;
                int i2 = Mathf.Min((int)t2, Nodos - 2);
                float u2 = t2 - i2;
                Vector3 siguiente = Suave(NodoSeguro(i2 - 1), NodoSeguro(i2),
                                          NodoSeguro(i2 + 1), NodoSeguro(i2 + 2), u2);

                Vector3 adelante = a < _anillosTubo - 1 ? siguiente - centro : centro - _vTuboCentroPrevio;
                if (adelante.sqrMagnitude < 1e-12f) adelante = Vector3.forward;
                adelante.Normalize();
                _vTuboCentroPrevio = centro;

                Vector3 lado = Vector3.Cross(arriba, adelante);
                if (lado.sqrMagnitude < 1e-6f) lado = Vector3.Cross(Vector3.right, adelante);
                lado.Normalize();
                arriba = Vector3.Cross(adelante, lado);

                for (int l = 0; l < LadosTubo; l++)
                {
                    float ang = l * Mathf.PI * 2f / LadosTubo;
                    Vector3 n = lado * Mathf.Cos(ang) + arriba * Mathf.Sin(ang);
                    int v = a * LadosTubo + l;
                    _vTubo[v] = centro + n * radio;
                    _nTubo[v] = n;
                }
            }

            _tubo.vertices = _vTubo;
            _tubo.normals = _nTubo;
            _tubo.RecalculateBounds();
        }

        private Vector3 _vTuboCentroPrevio;

        /// <summary>Longitud recorrida de verdad por el cable, para depurar.</summary>
        public float LongitudActual()
        {
            if (_pos == null) return 0f;

            // Se recorre por el tamaño REAL del arreglo, no por Nodos: si los dos se
            // desincronizan, lo que no debe pasar pero ya paso una vez, esto devuelve un
            // numero raro en vez de tirar una excepcion desde algo que solo informa.
            float total = 0f;
            for (int i = 0; i + 1 < _pos.Length; i++) total += Vector3.Distance(_pos[i], _pos[i + 1]);
            return total;
        }

        public Vector3 PosicionNodo(int i)
        {
            if (_pos == null || i < 0 || i >= _pos.Length) return Vector3.zero;
            return _pos[i];
        }

        /// <summary>Punto más bajo del cable, útil para comprobar que se apoya y no atraviesa.</summary>
        public float AlturaMinima()
        {
            if (_pos == null) return 0f;
            float min = float.MaxValue;
            for (int i = 0; i < Nodos; i++) min = Mathf.Min(min, _pos[i].y);
            return min;
        }
    }
}
