
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace VDF
{
    /// <summary>
    /// Locomoción de la escena VR: teletransporte por el suelo, estaciones fijas alrededor
    /// del módulo y giro por pasos.
    ///
    /// Por qué hace falta: el rig de XRI trae el <c>TeleportationProvider</c> y los dos
    /// interactores de teletransporte montados de fábrica, así que todo *parecía* estar
    /// hecho. Pero en la escena no había **ni un solo sitio al que teletransportarse**: cero
    /// áreas y cero anclas. El rayo salía, apuntaba al suelo y no pasaba nada, que es de los
    /// fallos que no dan ningún error en consola.
    ///
    /// Las estaciones son anclas y no solo área libre porque el alumno con el visor puesto
    /// no tiene por qué saber dónde conviene ponerse: el ancla lo deja a la distancia y con
    /// la orientación correctas para lo que va a hacer ahí.
    /// </summary>
    public static class VdfXrLocomocion
    {
        /// <summary>Una estación: dónde se pone el alumno y qué mira desde ahí.</summary>
        public struct Estacion
        {
            public string Nombre;
            public Vector3 Posicion;
            public Vector3 Mirando;

            public Estacion(string nombre, Vector3 posicion, Vector3 mirando)
            {
                Nombre = nombre;
                Posicion = posicion;
                Mirando = mirando;
            }
        }

        public const float RadioAncla = 0.26f;

        /// <summary>
        /// Las cuatro estaciones, con la distancia puesta según para qué sirve cada una y
        /// comprobada con una captura desde el ojo del alumno, no a ojo sobre el plano:
        ///
        ///  - Las de TRABAJO (teclado y salida a motor) van pegadas al borde del banco, que
        ///    está en z = -0,60. Los bornes y las teclas están en z ≈ 0, así que quedan a
        ///    unos 65 cm: distancia de brazo inclinándose un poco. La primera versión las
        ///    puso a 1 m «por seguridad» y desde ahí no se llegaba a tocar nada.
        ///  - La de VISTA GENERAL se separa 1,35 m para ver el tablero entero (1,10 × 0,85 m)
        ///    sin que su disco pise el de la estación del teclado. Los discos miden 52 cm:
        ///    dos anclas solapadas se roban el rayo entre ellas, igual que les pasaba a los
        ///    bornes del motor, y un test lo cazó antes de llegar a la escena.
        ///  - La del Ladder se separa 2 m del panel, que mide 1,74 m de alto: desde los 0,8 m
        ///    de la primera versión el diagrama no cabía en el campo de visión.
        /// </summary>
        public static List<Estacion> Estaciones()
        {
            return new List<Estacion>
            {
                new Estacion("Frente al tablero",  new Vector3( 0.00f, 0f, -0.90f), new Vector3( 0.00f, 1.05f, -0.25f)),
                new Estacion("Teclado del V1000",  new Vector3( 0.58f, 0f, -0.85f), new Vector3( 0.12f, 1.00f, -0.28f)),
                new Estacion("Salida a motor",     new Vector3(-0.62f, 0f, -0.90f), new Vector3(-0.46f, 0.95f, -0.38f)),
                new Estacion("Diagrama Ladder",    new Vector3(-0.20f, 0f, -1.95f), new Vector3( 0.00f, 1.29f, -0.32f))
            };
        }

        /// <summary>
        /// Monta todo. Devuelve un resumen para dejarlo en el log del editor.
        /// Es idempotente: volver a llamarlo no duplica nada.
        /// </summary>
        public static string Montar()
        {
            int areas = PrepararSuelo();
            int anclas = CrearEstaciones();
            string giro = AjustarGiro();
            string desliz = QuitarMovimientoContinuo();
            bool mesa = BloquearBajoLaMesa();

            return "teletransporte: " + areas + " area(s) de suelo, " + anclas + " estaciones; " + giro
                   + "; " + desliz + (mesa ? "; sin teletransporte bajo la mesa" : "");
        }

        /// <summary>
        /// Convierte el suelo de la sala en zona de teletransporte libre, para que el alumno
        /// pueda alejarse a mirar el conjunto y no solo saltar entre estaciones.
        /// </summary>
        public static int PrepararSuelo()
        {
            var suelo = GameObject.Find("Suelo");
            if (suelo == null) return 0;
            if (suelo.GetComponent<TeleportationArea>() != null) return 1;

            if (suelo.GetComponent<Collider>() == null) return 0;

            var area = suelo.AddComponent<TeleportationArea>();
            area.matchOrientation = MatchOrientation.WorldSpaceUp;
            area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            return 1;
        }

        /// <summary>Crea (o deja como están) las anclas de las estaciones.</summary>
        public static int CrearEstaciones()
        {
            var raiz = GameObject.Find("VDF_Teletransporte");
            if (raiz == null) raiz = new GameObject("VDF_Teletransporte");

            var estaciones = Estaciones();
            int creadas = 0;

            for (int i = 0; i < estaciones.Count; i++)
            {
                string nombre = "Estacion_" + estaciones[i].Nombre.Replace(" ", "_");
                var existente = raiz.transform.Find(nombre);

                GameObject nodo;
                if (existente != null)
                {
                    nodo = existente.gameObject;
                }
                else
                {
                    nodo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    nodo.name = nombre;
                    nodo.transform.SetParent(raiz.transform, false);
                    creadas++;
                }

                nodo.transform.position = estaciones[i].Posicion + new Vector3(0f, 0.005f, 0f);
                nodo.transform.localScale = new Vector3(RadioAncla * 2f, 0.005f, RadioAncla * 2f);

                // El ancla mira a lo que hay que mirar desde ella, en horizontal: inclinar
                // al alumno hacia arriba o hacia abajo al llegar marea.
                Vector3 hacia = estaciones[i].Mirando - estaciones[i].Posicion;
                hacia.y = 0f;
                if (hacia.sqrMagnitude > 0.0001f)
                    nodo.transform.rotation = Quaternion.LookRotation(hacia.normalized, Vector3.up);

                NormalizarColision(nodo);

                var ancla = nodo.GetComponent<TeleportationAnchor>();
                if (ancla == null) ancla = nodo.AddComponent<TeleportationAnchor>();
                ancla.matchOrientation = MatchOrientation.TargetUpAndForward;
                ancla.teleportAnchorTransform = nodo.transform;
                ancla.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            }

            return creadas;
        }

        /// <summary>
        /// Deja el disco con una colisión plana, del tamaño de lo que se ve.
        ///
        /// El cilindro primitivo de Unity viene con un <c>CapsuleCollider</c>, y el radio de
        /// una cápsula se escala con el MAYOR de X y Z, no con la Y. Al aplastar el disco a
        /// 1 cm de alto, el collider seguía siendo una cápsula de 64 cm: un obstáculo
        /// invisible a la altura de la rodilla en cada estación, que además se habría
        /// llevado por delante rayos y agarres que pasaran cerca. Medido con el rayo en Play
        /// Mode antes de verlo en el visor.
        /// </summary>
        private static void NormalizarColision(GameObject nodo)
        {
            var viejos = nodo.GetComponents<Collider>();
            for (int i = 0; i < viejos.Length; i++)
            {
                if (viejos[i] is BoxCollider) continue;
                Object.DestroyImmediate(viejos[i]);
            }

            var caja = nodo.GetComponent<BoxCollider>();
            if (caja == null) caja = nodo.AddComponent<BoxCollider>();

            // El cilindro primitivo mide 1 de diámetro y 2 de alto en unidades locales.
            caja.size = new Vector3(1f, 2f, 1f);
            caja.center = Vector3.zero;
            caja.isTrigger = false;
        }

        public static string QuitarMovimientoContinuo()
        {
            int mandos = 0, movedores = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb == null) continue;
                var tipo = mb.GetType();

                if (tipo.Name == "ControllerInputActionManager")
                {
                    var prop = tipo.GetProperty("smoothMotionEnabled");
                    // El setter reconfigura las acciones de entrada: solo en Play. Llamado en
                    // el editor daba «Assertion failed: ShouldRunBehaviour()» una vez por mando.
                    if (Application.isPlaying && prop != null && prop.CanWrite) prop.SetValue(mb, false);
                    mandos++;
#if UNITY_EDITOR
                    // El setter cambia el valor en memoria, pero el mando es parte de un
                    // PREFAB y un cambio por código no queda registrado como override: al
                    // guardar la escena volvía a true. Se vio porque el test seguía fallando
                    // tras reconstruir. Por SerializedObject sí se registra.
                    if (!Application.isPlaying)
                    {
                        var so = new UnityEditor.SerializedObject(mb);
                        var campo = so.FindProperty("m_SmoothMotionEnabled");
                        if (campo != null)
                        {
                            campo.boolValue = false;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
#endif
                }
                else if (tipo.Name == "DynamicMoveProvider" || tipo.Name == "ContinuousMoveProvider")
                {
                    mb.enabled = false;
                    movedores++;
                }
            }
            return "desplazamiento continuo quitado (" + mandos + " mandos, " + movedores + " proveedor)";
        }

        public const string NombreBloqueoMesa = "VDF_SinTeletransporteBajoLaMesa";

        public static bool BloquearBajoLaMesa()
        {
            var viejo = GameObject.Find(NombreBloqueoMesa);
            if (viejo != null) Object.DestroyImmediate(viejo);

            var encimera = GameObject.Find("Encimera");
            var r = encimera != null ? encimera.GetComponent<Renderer>() : null;
            if (r == null) return false;

            Bounds b = r.bounds;
            var go = new GameObject(NombreBloqueoMesa);
            go.transform.position = new Vector3(b.center.x, b.min.y * 0.5f, b.center.z);
            var caja = go.AddComponent<BoxCollider>();
            // Un poco más ancho que la encimera y por debajo de ella: no toca nada de lo que
            // hay encima, que es lo que se apunta y se coge.
            caja.size = new Vector3(b.size.x + 0.10f, Mathf.Max(0.05f, b.min.y - 0.01f), b.size.z + 0.10f);
            return true;
        }

        /// <summary>¿Queda algún mando que deslice al jugador? Para los tests.</summary>
        public static bool HayMovimientoContinuo()
        {
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb == null) continue;
                var tipo = mb.GetType();
                if (tipo.Name == "ControllerInputActionManager")
                {
                    var prop = tipo.GetProperty("smoothMotionEnabled");
                    if (prop != null && (bool)prop.GetValue(mb)) return true;
                }
                else if ((tipo.Name == "DynamicMoveProvider" || tipo.Name == "ContinuousMoveProvider") && mb.enabled)
                    return true;
            }
            return false;
        }

        public static string AjustarGiro()
        {
            var snap = Object.FindAnyObjectByType<SnapTurnProvider>(FindObjectsInactive.Include);
            var continuo = Object.FindAnyObjectByType<ContinuousTurnProvider>(FindObjectsInactive.Include);

            if (snap != null)
            {
                snap.enabled = true;
                snap.turnAmount = 45f;
            }
            if (continuo != null) continuo.enabled = false;

            return "giro por pasos de " + (snap != null ? snap.turnAmount.ToString("F0") : "?") +
                   " grados, continuo " + (continuo != null && continuo.enabled ? "activo" : "desactivado");
        }
    }
}
