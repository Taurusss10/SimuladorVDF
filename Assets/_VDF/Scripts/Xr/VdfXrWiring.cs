// NOTA IMPORTANTE DE UNITY
// Cada MonoBehaviour tiene que vivir en un archivo .cs con SU MISMO NOMBRE.
// Unity crea un unico MonoScript por archivo, con el nombre del archivo; cualquier
// MonoBehaviour cuya clase no coincida NO obtiene MonoScript y, por tanto, no se puede
// deserializar. Funciona dentro de la sesion (el tipo existe en memoria) y se rompe al
// reiniciar el editor, dejando "The referenced script is missing" en la escena.
// Ver E6 en VIABILIDAD.md: esto costo 71 componentes rotos y, probablemente, un crash.

using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Puente entre la interacción XR y el grafo de cableado de E4.
    ///
    /// Esto es lo que hace que el esfuerzo de E4 no se tire: la validación, el informe de
    /// faltantes y cortocircuitos y los tests son EXACTAMENTE los mismos en VR y en
    /// escritorio. Lo único que cambia es quién decide que dos bornes quedan unidos:
    /// allí un clic, aquí un conector encajado en un socket.
    /// </summary>
    public class VdfXrWiring : MonoBehaviour, IVdfFuenteCableado
    {
        [Tooltip("Controlador de cableado del que se toma el grafo. Si se deja vacío, se " +
                 "busca en la escena.")]
        public VdfJumperController Cableado;

        /// <summary>
        /// El grafo del cableado. **Es el mismo objeto que usa el modo escritorio**, no una
        /// copia.
        ///
        /// Antes esta clase creaba el suyo propio, y el resultado era que en VR había DOS
        /// grafos: el del controlador, que es el que lee el sistema de prácticas, y este,
        /// que es donde iban a parar los cables que el alumno enchufaba de verdad. Medido en
        /// Play Mode: el del controlador tenía 19 aristas y este 0, con los 20 cables XR
        /// sueltos. O sea que **en VR el alumno cableaba y la práctica no se enteraba**.
        ///
        /// Es la misma clase de fallo que la fase 4 arregló para los VISUALES del cable;
        /// aquí faltaba hacerlo con el grafo.
        /// </summary>
        public VdfWiringGraph Grafo
        {
            get
            {
                if (Cableado == null) Cableado = Object.FindAnyObjectByType<VdfJumperController>();
                if (Cableado != null) return Cableado.Grafo;

                // Sin controlador en la escena, se usa uno propio para no reventar.
                if (_propio == null) _propio = new VdfWiringGraph();
                return _propio;
            }
        }

        private VdfWiringGraph _propio;

        public event System.Action CableadoCambiado;

        private readonly List<VdfXrCable> _cables = new List<VdfXrCable>();

        private void Awake()
        {
            if (Cableado == null) Cableado = Object.FindAnyObjectByType<VdfJumperController>();
            if (_cables.Count == 0) Reindexar();
        }

        /// <summary>¿Se pueden coger los cables con el mando ahora mismo?</summary>
        public bool SePuedenCoger { get; private set; } = true;

        /// <summary>
        /// Deja o impide coger las puntas de cable con el mando.
        ///
        /// En las prácticas que no son de cablear, el gatillo lateral seguía cogiendo
        /// cables, y un alumno que solo quería mirar o pulsar teclas arrancaba uno sin
        /// querer. No se apaga el componente: ver <see cref="VdfFiltroAgarre"/>.
        /// </summary>
        public void PermitirCoger(bool si)
        {
            SePuedenCoger = si;
            var puntas = Object.FindObjectsByType<VdfXrPlug>(FindObjectsInactive.Include);
            for (int i = 0; i < puntas.Length; i++)
                VdfFiltroAgarre.Asegurar(puntas[i].gameObject, this);
        }

        /// <summary>¿Se ven los cables ahora mismo?</summary>
        public bool CablesVisibles { get; private set; } = true;

        /// <summary>
        /// Muestra u oculta los cables: el cuerpo, las puntas y los colliders de las puntas,
        /// para que el rayo no tropiece con un cable invisible. No se desactivan los objetos:
        /// el resto del puente y los tests los buscan con FindObjectsByType, que no ve los
        /// inactivos.
        /// </summary>
        public void MostrarCables(bool si)
        {
            CablesVisibles = si;
            if (_cables.Count == 0)
            {
                var encontrados = Object.FindObjectsByType<VdfXrCable>();
                for (int i = 0; i < encontrados.Length; i++) _cables.Add(encontrados[i]);
            }
            for (int i = 0; i < _cables.Count; i++)
            {
                var cable = _cables[i];
                if (cable == null) continue;
                MostrarObjeto(cable.gameObject, si);
                if (cable.ExtremoA != null) MostrarObjeto(cable.ExtremoA.gameObject, si);
                if (cable.ExtremoB != null) MostrarObjeto(cable.ExtremoB.gameObject, si);
            }
        }

        private static void MostrarObjeto(GameObject go, bool si)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = si;
            if (go.GetComponent<VdfXrPlug>() != null)
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = si;
        }

        public void Registrar(VdfXrCable cable)
        {
            if (cable != null && !_cables.Contains(cable)) _cables.Add(cable);
        }

        /// <summary>
        /// Vuelve a localizar los cables de la escena.
        ///
        /// La lista NO se serializa a propósito: se reconstruye al arrancar. Guardar
        /// referencias cruzadas a objetos de escena construidos por código resultó ser
        /// justo la fuente de los problemas de E5 y E6.
        /// </summary>
        public void Reindexar()
        {
            _cables.Clear();
            var encontrados = Object.FindObjectsByType<VdfXrCable>();
            for (int i = 0; i < encontrados.Length; i++) _cables.Add(encontrados[i]);
            Resincronizar();

            // Si alguien pidió ocultarlos antes de que existiera la lista (el runner arranca
            // la práctica 1 en su Awake, que puede correr antes que este), se aplica ahora.
            if (!CablesVisibles) MostrarCables(false);
        }

        /// <summary>Aristas que puso este puente la última vez que se resincronizó.</summary>
        private readonly List<VdfConnection> _mias = new List<VdfConnection>();

        /// <summary>
        /// Recalcula las aristas de los cables XR a partir de dónde está enchufado cada
        /// extremo.
        ///
        /// Se recalcula entero en vez de ir añadiendo y quitando según los eventos: es O(n)
        /// con n = número de cables (aquí, 20), y evita la clase de bug en la que el grafo y
        /// la escena se desincronizan porque se perdió un evento de desenchufe.
        ///
        /// Pero **solo retira lo que puso este puente**. Antes hacía `Grafo.Limpiar()`, y
        /// eso valía cuando el grafo era suyo; ahora lo comparte con el modo escritorio, así
        /// que vaciarlo entero borraría también los cables hechos con el ratón y los del
        /// cableado automático de la práctica.
        /// </summary>
        public void Resincronizar()
        {
            var grafo = Grafo;

            for (int i = 0; i < _mias.Count; i++)
                grafo.Desconectar(_mias[i].A, _mias[i].B);
            _mias.Clear();

            for (int i = 0; i < _cables.Count; i++)
            {
                if (_cables[i] == null) continue;

                // Que el cable con física sepa también qué bornes une. Desde la fase 4 es
                // el mismo tipo de objeto que crea el modo escritorio, así que con los Ids
                // puestos el borrado y la validación funcionan igual en los dos modos.
                _cables[i].SincronizarIds();

                string a, b;
                if (_cables[i].EstaCompleto(out a, out b) && grafo.Conectar(a, b))
                    _mias.Add(new VdfConnection(a, b));
            }

            var h = CableadoCambiado;
            if (h != null) h();

            // Avisar también por el canal del controlador: es a quien escucha
            // VdfSimulacion para revalidar el cableado y refrescar el informe.
            if (Cableado != null) Cableado.AvisarCableadoCambiado();
        }

        public VdfWiringReport Validar()
        {
            return VdfWiringSpec.Validar(Grafo);
        }

        /// <summary>
        /// Desenchufa todos los cables y los devuelve a la bandeja. Devuelve cuántos extremos
        /// soltó.
        ///
        /// Es el equivalente en VR de <see cref="VdfJumperController.BorrarTodo"/>, y hace
        /// falta por lo mismo: una práctica de cableado tiene que empezar con el módulo sin
        /// cablear. Vaciando solo el grafo, el alumno se encontraba el tablero lleno de cables
        /// y la práctica dada por hecha.
        /// </summary>
        public int SoltarTodos()
        {
            ApagarSocketsUnMomento();
            int soltados = 0;
            for (int i = 0; i < _cables.Count; i++)
            {
                if (_cables[i] == null) continue;
                soltados += Soltar(_cables[i].ExtremoA);
                soltados += Soltar(_cables[i].ExtremoB);
                // De vuelta en la bandeja, su largo de bandeja: con el que tenían en el
                // tablero hacían bucles enormes por toda la mesa.
                _cables[i].AjustarLargoAlDestino();
            }
            Resincronizar();
            if (Application.isPlaying) VdfCablePhysics.AsentarTodos(30);
            return soltados;
        }

        public bool DesenchufarUnExtremo(string a, string b)
        {
            for (int i = 0; i < _cables.Count; i++)
            {
                var cable = _cables[i];
                string x, y;
                if (cable == null || !cable.EstaCompleto(out x, out y)) continue;
                if (!((x == a && y == b) || (x == b && y == a))) continue;

                var punta = cable.ExtremoA != null && cable.ExtremoA.Conectado != null &&
                            cable.ExtremoA.Conectado.Id == b ? cable.ExtremoA : cable.ExtremoB;
                var borne = punta.Conectado;
                ApagarSocketsUnMomento();
                Soltar(punta);
                if (borne != null)
                    punta.transform.position = DondeDejarLaPuntaSuelta(borne.transform);
                Resincronizar();
                return true;
            }
            return false;
        }

        public static Vector3 DondeDejarLaPuntaSuelta(Transform borne)
        {
            Vector3 p = borne.position - borne.forward * 0.08f + Vector3.down * 0.05f;
            float minimo = VdfReconstructorEscena.AlturaEncimera + 0.03f;
            if (p.y < minimo) p.y = minimo;
            return p;
        }

        /// <summary>Segundos que los bornes no aceptan puntas tras soltarlas desde el código.</summary>
        public const float PausaSockets = 0.3f;

        private float _reactivarSocketsEn = -1f;

        /// <summary>¿Están los bornes sin aceptar puntas ahora mismo? Para los tests.</summary>
        public bool SocketsEnPausa { get { return _reactivarSocketsEn > 0f; } }

        private void ApagarSocketsUnMomento()
        {
            if (!Application.isPlaying) return;
            var sockets = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            for (int i = 0; i < sockets.Length; i++) sockets[i].socketActive = false;
            _reactivarSocketsEn = Time.unscaledTime + PausaSockets;
        }

        private void Update()
        {
            if (_reactivarSocketsEn < 0f || Time.unscaledTime < _reactivarSocketsEn) return;
            _reactivarSocketsEn = -1f;
            var sockets = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            for (int i = 0; i < sockets.Length; i++) sockets[i].socketActive = true;
        }

        private static int Soltar(VdfXrPlug extremo)
        {
            if (extremo == null) return 0;

            var agarre = extremo.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (agarre != null && agarre.isSelected && agarre.interactionManager != null)
            {
                var quienes = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(agarre.interactorsSelecting);
                foreach (var quien in quienes)
                    agarre.interactionManager.SelectExit(quien, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)agarre);
            }

            extremo.Conectado = null;
            if (extremo.Reposo != Vector3.zero) extremo.transform.position = extremo.Reposo;
            return 1;
        }

        /// <summary>
        /// Deja el módulo cableado correctamente, enchufando los cables de la bandeja uno a uno.
        /// Devuelve cuántos usó.
        ///
        /// Sirve para las prácticas que empiezan con el módulo ya cableado (la de operación,
        /// por ejemplo), donde cablear a mano los diecinueve cables no es lo que se enseña.
        /// Si no hay cables suficientes en la bandeja, enchufa los que haya y lo dice el
        /// número devuelto.
        /// </summary>
        public int EnchufarCableadoCorrecto()
        {
            SoltarTodos();

            var bornes = new Dictionary<string, VdfTerminal>();
            var todos = Object.FindObjectsByType<VdfTerminal>();
            for (int i = 0; i < todos.Length; i++)
                if (!string.IsNullOrEmpty(todos[i].Id)) bornes[todos[i].Id] = todos[i];

            var redes = VdfWiringSpec.Practica();
            int siguiente = 0;

            for (int i = 0; i < redes.Count; i++)
            {
                var red = redes[i];
                for (int j = 0; j + 1 < red.Bornes.Length; j++)
                {
                    if (siguiente >= _cables.Count) return siguiente;

                    VdfTerminal a, b;
                    if (!bornes.TryGetValue(red.Bornes[j], out a)) continue;
                    if (!bornes.TryGetValue(red.Bornes[j + 1], out b)) continue;

                    var cable = _cables[siguiente];
                    if (cable == null || cable.ExtremoA == null || cable.ExtremoB == null) continue;

                    cable.ExtremoA.Conectado = a;
                    cable.ExtremoB.Conectado = b;
                    cable.ExtremoA.transform.position = a.transform.position;
                    cable.ExtremoB.transform.position = b.transform.position;
                    siguiente++;
                }
            }

            // Cada cable, al largo que le toca para el salto que tiene que dar. Sin esto
            // quedan estirados al limite y salen rectos como cuerdas.
            for (int i = 0; i < _cables.Count && i < siguiente; i++)
                if (_cables[i] != null) _cables[i].AjustarLargoAlDestino();

            Resincronizar();

            // Un empujon corto para que el primer fotograma ya se vea decente, y el resto
            // lo termina la fisica normal.
            //
            // Aqui arriba se mueven las PUNTAS a sus bornes, pero el cuerpo del cable es un
            // sistema de nodos que estaba dormido. Hay que despertarlo o se queda donde
            // estaba, y lo que se ve son enchufes flotando y cables tiesos.
            //
            // El numero NO es 180, y eso tiene su historia. Con 180 pasos el cableado
            // completo costaba 1.128 ms: mas de un segundo CONGELADO cada vez que el alumno
            // elige una practica. En un computador se nota; en un visor, con la imagen
            // clavada un segundo delante de los ojos, es de las cosas que marean.
            //
            // Con 30 baja a unos 200 ms y el resultado es el mismo, porque desde que los
            // cables despiertan al moverseles un extremo la fisica los sigue asentando sola,
            // repartida entre fotogramas y sin que nadie lo note. Medido: con 180 pasos y con
            // 30 se acaba igual, cero cables tensos y holgura media 1,80.
            VdfCablePhysics.AsentarTodos(30);

            return siguiente;
        }

        public int CablesRegistrados { get { return _cables.Count; } }
    }
}
