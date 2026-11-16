using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VDF.Tests
{
    public class VdfEscenaVrTests
    {
        private const string EscenaVr = "VDF_VR";

        private VdfSimulacion _sim;
        private VdfXrWiring _xr;

        [UnitySetUp]
        public IEnumerator Preparar()
        {
            VdfSimulacion.ModoMandoForzado = VdfModoMando.Pulsadores; // estos tests recorren la logica de pulsadores
            SceneManager.LoadScene(EscenaVr, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.25f);

            _sim = Object.FindAnyObjectByType<VdfSimulacion>();
            _xr = Object.FindAnyObjectByType<VdfXrWiring>();

            Assert.IsNotNull(_sim, "la escena de VR no tiene VdfSimulacion");
            _sim.Despertar();
            yield return null;

            // La escena abre en la práctica 1, que oculta los cables. Casi todas estas
            // pruebas los manejan directamente sin elegir práctica, así que se muestran; la
            // que comprueba la ocultación elige la práctica ella misma.
            if (_xr != null) _xr.MostrarCables(true);
        }

        /// <summary>
        /// La escena de VR tiene su puente de cableado y cables de verdad que coger.
        /// Sin cables registrados, la práctica 3 en VR no existe.
        /// </summary>
        [UnityTest]
        public IEnumerator LaEscenaDeVr_TieneCableadoYCables()
        {
            Assert.IsNotNull(_xr, "la escena de VR no tiene VdfXrWiring: el cableado del "
                                + "visor no estaría conectado a nada");

            _xr.Reindexar();
            yield return null;

            Assert.Greater(_xr.CablesRegistrados, 0,
                "no hay ni un cable que coger en la escena de VR");
        }

        /// <summary>
        /// Hay cables suficientes para completar el cableado de la práctica.
        ///
        /// Si la bandeja tiene menos cables que uniones pide el esquema, la práctica 3 es
        /// imposible de terminar en VR y el alumno no tendría forma de saber por qué: se
        /// quedaría sin cables a medias.
        /// </summary>
        [UnityTest]
        public IEnumerator HayCablesSuficientes_ParaElCableadoCompleto()
        {
            if (_xr == null) Assert.Ignore("la escena de VR no tiene VdfXrWiring");

            _xr.Reindexar();
            yield return null;

            int necesarios = VdfWiringSpec.CablesMinimos(VdfWiringSpec.Practica());
            Assert.GreaterOrEqual(_xr.CablesRegistrados, necesarios,
                "el esquema necesita " + necesarios + " cables y en la escena de VR solo hay "
                + _xr.CablesRegistrados + ": la práctica 3 no se podría terminar");
        }

        [UnityTest]
        public IEnumerator ElCableadoDeVr_LlegaHastaLaValidacion()
        {
            if (_xr == null) Assert.Ignore("la escena de VR no tiene VdfXrWiring");

            _xr.Reindexar();
            int enchufados = _xr.EnchufarCableadoCorrecto();
            yield return null;

            Assert.Greater(enchufados, 0, "no se enchufó ningún cable");

            var informe = _xr.Validar();
            Assert.IsTrue(informe.EsCorrecto,
                "enchufando el cableado correcto en VR la validación debería dar correcto. "
                + "Faltan " + informe.Faltantes.Count + ", sobran " + informe.Sobrantes.Count);

            _xr.SoltarTodos();
            yield return null;

            Assert.IsFalse(_xr.Validar().EsCorrecto,
                "soltando todos los cables ya no puede estar correcto");
        }

        /// <summary>
        /// Y lo que de verdad importa: que la SIMULACIÓN se entere del cableado de VR, no
        /// solo el puente. Es la diferencia entre que la práctica 3 tenga consecuencias o
        /// sea decorado.
        /// </summary>
        [UnityTest]
        public IEnumerator LaSimulacionSeEntera_DelCableadoHechoEnVr()
        {
            if (_xr == null) Assert.Ignore("la escena de VR no tiene VdfXrWiring");

            _xr.Reindexar();
            _xr.EnchufarCableadoCorrecto();
            _sim.RevisarCableado();
            yield return null;

            Assert.IsNotNull(_sim.Cables,
                "la simulación de VR debería tener una fuente de cableado");
            Assert.IsTrue(_sim.Cables.Validar().EsCorrecto,
                "la simulación debería ver el cableado que se hizo en VR. " + _sim.Resumen());
        }

        [UnityTest]
        public IEnumerator EnVr_SoltarTodoDejaElModuloSinCablear()
        {
            if (_xr == null) Assert.Ignore("la escena de VR no tiene VdfXrWiring");

            // Punto de partida de una practica que empieza cableada.
            _sim.SaltarCableado = true;
            _sim.AplicarPuntoDePartida();
            yield return null;
            Assert.IsTrue(_sim.Cables.Validar().EsCorrecto,
                "una practica que empieza cableada deberia arrancar con el cableado correcto");

            // El alumno lo suelta todo.
            _xr.SoltarTodos();
            _sim.RevisarCableado();
            yield return null;

            Assert.AreEqual(0, _xr.Grafo.Cuenta,
                "tras soltar todos los cables no deberia quedar NINGUNA union en el grafo. "
                + "Si quedan, son cables de la via del raton que el puente de VR no retira, "
                + "y el modulo seguiria funcionando con la bandeja llena.");
            Assert.IsFalse(_sim.Cables.Validar().EsCorrecto,
                "y el cableado no puede seguir dando correcto");
        }

        /// <summary>
        /// Una practica que empieza SIN cablear arranca sin cables, tambien en VR.
        /// Si arrancara cableada, la practica 3 estaria hecha antes de empezar.
        /// </summary>
        [UnityTest]
        public IEnumerator EnVr_UnaPracticaSinCablear_ArrancaSinCables()
        {
            if (_xr == null) Assert.Ignore("la escena de VR no tiene VdfXrWiring");

            _sim.SaltarCableado = false;
            _sim.AplicarPuntoDePartida();
            yield return null;

            Assert.AreEqual(0, _xr.Grafo.Cuenta,
                "la practica de cableado tiene que empezar con el modulo sin cablear");
            Assert.IsFalse(_sim.Cables.Validar().EsCorrecto);
        }

        /// <summary>
        /// Todo borne del esquema tiene su socket en la escena de VR.
        ///
        /// Un borne sin socket no se puede enchufar con el mando: se vería, se intentaría y
        /// no pasaría nada. Es el equivalente en VR a que falte un borne en el tablero.
        /// </summary>
        [UnityTest]
        public IEnumerator TodoBorneDelEsquema_TieneSocketEnVr()
        {
            var sockets = Object.FindObjectsByType<VdfXrSocket>(FindObjectsInactive.Include);
            if (sockets.Length == 0) Assert.Ignore("la escena de VR no tiene sockets");

            var conSocket = new System.Collections.Generic.HashSet<string>();
            foreach (var s in sockets)
                if (s != null && s.Borne != null && !string.IsNullOrEmpty(s.Borne.Id))
                    conSocket.Add(s.Borne.Id);

            var faltan = new System.Collections.Generic.List<string>();
            foreach (var red in VdfWiringSpec.Practica())
                foreach (string id in red.Bornes)
                    if (!conSocket.Contains(id) && !faltan.Contains(id))
                        faltan.Add(id);

            Assert.IsEmpty(faltan,
                "estos bornes del esquema no tienen socket en VR, así que no se podrían "
                + "enchufar con el mando: " + string.Join(", ", faltan.ToArray()));
            yield return null;
        }

        /// <summary>
        /// La práctica 4 se puede operar en la escena de VR igual que en escritorio.
        /// Los pulsadores del tablero son los mismos objetos; lo que cambia es quién los
        /// acciona, y eso no debería alterar la lógica.
        /// </summary>
        [UnityTest]
        public IEnumerator EnVr_ElModuloSeOperaIgual()
        {
            if (_xr != null)
            {
                _xr.Reindexar();
                _xr.EnchufarCableadoCorrecto();
                _sim.RevisarCableado();
                yield return null;
            }

            _sim.PulsarStop();
            yield return null;
            _sim.PulsarStart();
            _sim.PulsarSubir();

            float limite = PlcFanLogic.RetardoCambioNivelSegundos + 2f;
            float t = 0f;
            while (_sim.Plc.NivelActivo < 1 && t < limite)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(1, _sim.Plc.NivelActivo,
                "en VR el módulo debería arrancar igual que en escritorio. " + _sim.Resumen());

            _sim.PulsarEmergencia();
            yield return null;
            Assert.AreEqual(0, _sim.Plc.NivelActivo, "y el hongo debería parar igual");
        }

        [UnityTest]
        public IEnumerator EnVr_LosPulsadoresDelTableroEstanAccionables()
        {
            var mandos = Object.FindObjectsByType<VdfPulsadorTablero>(FindObjectsInactive.Include);

            Assert.Greater(mandos.Length, 0,
                "en la escena de VR no hay ni un pulsador accionable en el tablero");

            int sinSimulacion = 0;
            foreach (var m in mandos)
                if (m != null && m.Simulacion == null) sinSimulacion++;

            Assert.AreEqual(0, sinSimulacion,
                sinSimulacion + " pulsadores de VR no apuntan a la simulación: al pulsarlos "
                + "no pasaría nada");
            yield return null;
        }

        /// <summary>
        /// El teclado del variador existe en VR, que es lo que necesita la práctica 2.
        /// </summary>
        [UnityTest]
        public IEnumerator EnVr_ElTecladoDelVariadorExiste()
        {
            var teclado = Object.FindAnyObjectByType<VfdKeypadView>();
            Assert.IsNotNull(teclado,
                "sin teclado en VR la práctica 2 no se puede hacer con el visor");
            yield return null;
        }

        /// <summary>
        /// Las cinco prácticas se pueden empezar en la escena de VR. No comprueba que se
        /// terminen —eso ya lo hace el recorrido en escritorio y la lógica es la misma—,
        /// sino que ninguna depende de algo que solo exista en la escena de escritorio.
        /// </summary>
        [UnityTest]
        public IEnumerator EnVr_LasCincoPracticasSePuedenEmpezar()
        {
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            Assert.IsNotNull(runner, "la escena de VR no tiene VdfPracticaRunner");

            foreach (var practica in VdfPracticasDefaults.CrearTodas())
            {
                runner.Empezar(practica);
                yield return null;

                Assert.Greater(runner.TotalPasos, 0,
                    "la práctica '" + practica.Titulo + "' no tiene pasos en VR");
                Assert.IsNotNull(runner.Contexto, "y debería tener contexto");
            }
        }

        [UnityTest]
        public IEnumerator LaEscena_NoLlevaPiezasDeDesarrolloDentro()
        {
            yield return null;

            string[] prohibidos =
            {
                "XR Interaction Simulator",
                "XR Interaction Simulator UI",
            };

            var encontrados = new List<string>();
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                string n = go.name.Replace("(Clone)", string.Empty).Trim();
                foreach (var p in prohibidos)
                    if (n == p && !encontrados.Contains(n)) encontrados.Add(n);
            }

            // El simulador SÍ puede aparecer al entrar en Play desde el editor: es su trabajo,
            // y el ajuste que lo crea está marcado como "solo en el editor", así que no viaja
            // en el APK. Lo que no puede es estar GUARDADO en la escena, y eso se mira en el
            // archivo, no en memoria.
            var ruta = "Assets/_VDF/Scenes/" + EscenaVr + ".unity";
            string texto = System.IO.File.Exists(ruta) ? System.IO.File.ReadAllText(ruta) : null;
            if (texto != null)
            {
                var guardados = new List<string>();
                foreach (var p in prohibidos)
                    if (texto.Contains(p)) guardados.Add(p);

                Assert.IsEmpty(guardados,
                    "la escena " + EscenaVr + " tiene guardadas piezas de desarrollo que acabarían "
                    + "dentro del instalable: " + string.Join(", ", guardados.ToArray())
                    + ". Ejecuta VDF > Reconstruir las dos escenas y guarda.");
            }
        }

        /// <summary>
        /// Ningún borne puede robarle el cable al de al lado.
        ///
        /// Un socket captura una punta cuando los dos volúmenes se tocan, así que el alcance
        /// real de captura es la SUMA del radio del socket y el del conector. Si esa suma
        /// llega al borne vecino, los dos sockets quieren la misma punta y **gana el que se
        /// procese antes, no el más cercano**: el resultado es aleatorio.
        ///
        /// Pasaba de verdad. Los radios eran 8,5 y 14 mm, o sea 22,5 mm de alcance, contra
        /// 18 mm de separación mínima entre bornes. Al cargar la escena de VR, el cable que
        /// va de BRK-L2 a S/L2 acababa en BAJAR-A, que está a 18 mm de S/L2; eso partía en
        /// dos la fase L2 y dejaba cinco uniones sin efecto. Y lo peor no era el arranque:
        /// con ese alcance, **un alumno que enchufara bien en S/L2 podía quedar conectado a
        /// BAJAR-A** y ver "mal cableado" habiéndolo hecho bien.
        ///
        /// Nada de esto se veía desde el computador: es la escena de VR y el desarrollador no
        /// tiene visor. Por eso el test mide la geometría en vez de jugar la práctica.
        /// </summary>
        [UnityTest]
        public IEnumerator NingunBorne_PuedeRobarleElCableAlVecino()
        {
            yield return null;

            float radioPunta = 0f;
            foreach (var plug in Object.FindObjectsByType<VdfXrPlug>(FindObjectsInactive.Include))
            {
                var esfera = plug.GetComponent<SphereCollider>();
                if (esfera == null) continue;
                float r = esfera.radius * plug.transform.lossyScale.x;
                if (r > radioPunta) radioPunta = r;
            }
            Assert.Greater(radioPunta, 0f, "la escena de VR no tiene conectores con esfera");

            // Cada socket contra el borne MÁS CERCANO A ÉL, no el mayor de la escena contra
            // la pareja más junta de la escena. Desde que el PLC se dibuja 1,6 veces más
            // grande, sus sockets miden 12 mm de radio, pero sus vecinos están a 29 mm; la
            // comparación global los enfrentaba a los 18 mm del tablero y daba un falso
            // fallo.
            var sockets = new List<MonoBehaviour>();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (mb != null && mb.GetType().Name == "XRSocketInteractor" && mb.GetComponent<SphereCollider>() != null)
                    sockets.Add(mb);
            Assert.Greater(sockets.Count, 10, "la escena de VR no tiene sockets con esfera");

            string peor = null;
            float peorHolgura = float.MaxValue;
            for (int i = 0; i < sockets.Count; i++)
                for (int j = i + 1; j < sockets.Count; j++)
                {
                    var a = sockets[i]; var b = sockets[j];
                    float ra = a.GetComponent<SphereCollider>().radius * a.transform.lossyScale.x;
                    float rb = b.GetComponent<SphereCollider>().radius * b.transform.lossyScale.x;
                    // Con la punta centrada en uno, no puede tocar la esfera del otro.
                    float alcance = Mathf.Max(ra, rb) + radioPunta;
                    float d = Vector3.Distance(a.transform.position, b.transform.position);
                    float holgura = d - alcance;
                    if (holgura < peorHolgura)
                    {
                        peorHolgura = holgura;
                        peor = a.name + " y " + b.name + ": alcance " + (alcance * 1000f).ToString("F1")
                             + " mm, separados " + (d * 1000f).ToString("F1") + " mm";
                    }
                }

            Assert.Greater(peorHolgura, 0f,
                "el alcance de captura llega al borne vecino: " + peor + ". Con esto, enchufar "
                + "en uno puede registrar el otro y el alumno vería 'mal cableado' habiéndolo hecho bien");
        }
        /// <summary>
        /// Todo lo que se toca en el tablero tiene que responder TAMBIEN con el visor puesto.
        ///
        /// Este test existe por el segundo callejon sin salida que aparecio el 9 de
        /// noviembre, y es gemelo del de los pasos manuales: los cinco pulsadores del modulo
        /// -START, STOP, subir, bajar y la parada de emergencia- tenian su collider, se veian
        /// perfectos y NO TENIAN NADA QUE LOS ESCUCHARA en VR. En escritorio funcionaban
        /// desde siempre porque el raton los alcanza con un raycast.
        ///
        /// Con eso, la PRACTICA 4 ENTERA -dar tension, arrancar, subir las cuatro
        /// velocidades, bajar y parar- y buena parte de la 5 eran imposibles de terminar con
        /// las gafas puestas. Dos de las cinco practicas.
        ///
        /// Paso desapercibido porque las teclas del V1000 si tenian su puente y los botones
        /// del menu el suyo: el tablero parecia completo. A los pulsadores, sencillamente,
        /// nadie les puso el suyo.
        /// </summary>
        [UnityTest]
        public IEnumerator TodoLoQueSeTocaResponde_TambienEnElVisor()
        {
            yield return null;

            var sinPuente = new List<string>();

            foreach (var p in Object.FindObjectsByType<VdfPulsadorTablero>(FindObjectsInactive.Include))
            {
                bool interactuable = false, puente = false;
                foreach (var mb in p.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    if (mb.GetType().Name == "XRSimpleInteractable") interactuable = true;
                    if (mb is VdfXrPulsador) puente = true;
                }
                if (!interactuable || !puente) sinPuente.Add(p.name);
            }

            Assert.IsEmpty(sinPuente,
                "estos pulsadores no responden con el visor puesto: "
                + string.Join(", ", sinPuente.ToArray())
                + ". Sin ellos la practica 4 entera es imposible de terminar en VR");

            // Y las teclas del variador, que son la practica 2
            int teclas = 0, conPuente = 0;
            foreach (var b in Object.FindObjectsByType<VfdKeyButton>(FindObjectsInactive.Include))
            {
                teclas++;
                if (b.GetComponent<VdfXrKeyPoke>() != null) conPuente++;
            }
            Assert.Greater(teclas, 0, "la escena de VR no tiene teclas del variador");
            Assert.AreEqual(teclas, conPuente,
                "hay teclas del V1000 sin puente de XR: la practica 2 no se podria hacer");
        }

        [UnityTest]
        public IEnumerator AlAbrirLaApp_LaPractica1ArrancaComoEstaDefinida()
        {
            if (_xr == null) Assert.Ignore("la escena de VR no tiene VdfXrWiring");

            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            Assert.IsNotNull(runner, "la escena de VR no tiene runner de prácticas");
            Assert.IsNotNull(runner.Practica, "la guía arranca sin práctica");
            Assert.AreEqual(1, runner.Practica.Numero, "la app tiene que abrir en la práctica 1");

            // Y con el guion vigente, no con el .asset viejo de Data/Practicas.
            var delMenu = Object.FindAnyObjectByType<VdfMenuPracticas>().Practicas[0];
            Assert.AreEqual(delMenu.Paso(1).Instruccion, runner.Practica.Paso(1).Instruccion,
                "la guía abre con un guion distinto del que sale al elegir la práctica en el menú");

            Assert.AreEqual(0, _xr.Grafo.Cuenta,
                "el reconocimiento arranca con cables puestos: tapan los bornes que hay "
                + "que señalar y se pueden arrancar sin querer");

            Assert.IsFalse(_xr.SePuedenCoger, "en el reconocimiento no se cablea");

            var sinFiltro = new List<string>();
            foreach (var punta in Object.FindObjectsByType<VdfXrPlug>(FindObjectsInactive.Include))
            {
                if (!VdfFiltroAgarre.EstaRegistrado(punta.gameObject)) sinFiltro.Add(punta.name);
            }
            Assert.IsEmpty(sinFiltro,
                "en el reconocimiento no se cablea, pero estos cables se pueden coger "
                + "con el mando: " + string.Join(", ", sinFiltro.ToArray()));

            // En la práctica 2 el tablero va cableado y NO se puede coger nada; y lo
            // importante: bloquear el agarre no puede soltar los cables de sus bornes.
            var menu = Object.FindAnyObjectByType<VdfMenuPracticas>();
            runner.Empezar(menu.Practicas[1]);
            yield return null;
            yield return null;
            Assert.IsFalse(_xr.SePuedenCoger, "en la práctica 2 no se cablea");
            Assert.IsTrue(_sim.Cables.Validar().EsCorrecto,
                "al bloquear el agarre se soltaron cables: la práctica 2 tiene que seguir cableada");

            // Y al pasar a la práctica de cableado, los cables vuelven a cogerse.
            runner.Empezar(menu.Practicas[2]);
            yield return null;
            Assert.IsTrue(_xr.SePuedenCoger,
                "en la práctica de cableado los cables tienen que poder cogerse");
        }

        [UnityTest]
        public IEnumerator LasTeclasYLosPulsadores_LosVenElRayoYElToque()
        {
            // Como el alumno: primero cierra la bienvenida, que está delante del tablero.
            var bienvenida = Object.FindAnyObjectByType<VdfPanelBienvenida>();
            if (bienvenida != null) bienvenida.Cerrar();
            yield return null;

            var objetivos = new List<GameObject>();
            foreach (var k in Object.FindObjectsByType<VfdKeyButton>()) objetivos.Add(k.gameObject);
            foreach (var p in Object.FindObjectsByType<VdfXrPulsador>()) objetivos.Add(p.gameObject);
            Assert.Greater(objetivos.Count, 5, "no se encontraron teclas ni pulsadores");

            var fallos = new List<string>();
            foreach (var go in objetivos)
            {
                var col = go.GetComponent<Collider>();
                if (col == null) { fallos.Add(go.name + " (sin collider)"); continue; }
                if (col.isTrigger) fallos.Add(go.name + " (trigger: el rayo no lo ve)");
                else if (!VdfXrSetup.EsAccionable(go))
                    fallos.Add(go.name + " (sin XRPokeFilter: el toque no lo pulsa)");

                // Desde donde estaría la mano del alumno: delante y algo por encima.
                Vector3 centro = col.bounds.center;
                Vector3 origen = centro + new Vector3(0f, 0.20f, -0.45f);
                RaycastHit hit;
                if (!Physics.Raycast(origen, (centro - origen).normalized, out hit, 2f,
                                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    fallos.Add(go.name + " (el rayo no da en nada)");
                else if (hit.collider != col)
                    fallos.Add(go.name + " (lo tapa " + hit.collider.name + ")");
            }

            Assert.IsEmpty(fallos, "con el visor no se pueden accionar: "
                                   + string.Join(", ", fallos.ToArray()));
        }

        /// <summary>
        /// Todo lo tocable de la escena lo alcanza el mando TAL COMO ESTÁ CONFIGURADO.
        /// Ver <see cref="VdfAuditoriaXr"/>: no comprueba que exista el puente, comprueba que
        /// el rayo y el toque reales lleguen.
        /// </summary>
        [UnityTest]
        public IEnumerator TodoLoTocable_LoAlcanzaElMandoComoEstaConfigurado()
        {
            var bienvenida = Object.FindAnyObjectByType<VdfPanelBienvenida>();
            if (bienvenida != null) bienvenida.Cerrar();
            yield return null;

            var problemas = VdfAuditoriaXr.Auditar();
            Assert.IsEmpty(problemas, problemas.Count + " problemas con el mando:\n  "
                                      + string.Join("\n  ", problemas.ToArray()));
        }

        /// <summary>
        /// En el visor, apretar el gatillo sobre dos bornes NO tiende un cable.
        ///
        /// El gatillo llamaba al clic del ratón, que en la práctica 3 une los dos bornes
        /// señalados. En el visor eso creaba cables fantasma —en el grafo pero no en la
        /// escena— y la práctica de cableado se podía completar sin coger un solo cable.
        /// </summary>
        [UnityTest]
        public IEnumerator EnElVisor_SenalarDosBornes_NoTiendeUnCable()
        {
            if (_xr == null) Assert.Ignore("la escena de VR no tiene VdfXrWiring");

            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            var menu = Object.FindAnyObjectByType<VdfMenuPracticas>();
            runner.Empezar(menu.Practicas[2]);
            yield return null;
            yield return null;
            int antes = _xr.Grafo.Cuenta;

            VdfXrSenalarBorne a = null, b = null;
            foreach (var s in Object.FindObjectsByType<VdfXrSenalarBorne>())
            {
                if (s.Borne != null && s.Borne.Id == "BRK-L1") a = s;
                if (s.Borne != null && s.Borne.Id == "R/L1") b = s;
            }
            Assert.IsNotNull(a); Assert.IsNotNull(b);

            a.Senalar();
            b.Senalar();
            yield return null;

            Assert.AreEqual(antes, _xr.Grafo.Cuenta,
                "dos gatillazos sobre dos bornes crearon una conexión sin cable");
        }

        /// <summary>
        /// El mando queda ajustado al simulador: el gatillo pulsa (no solo el grip), el
        /// agarre no hace de imán y ningún joystick desliza. Ver VdfAjustesMando.
        /// </summary>
        [UnityTest]
        public IEnumerator ElMando_EstaAjustadoAlSimulador()
        {
            yield return null;
            yield return null;
            string problemas = VdfAjustesMando.Revisar();
            Assert.IsEmpty(problemas, problemas);
        }

        /// <summary>
        /// Las prácticas avanzan POR LOS PUENTES DEL MANDO, no llamando a la lógica.
        ///
        /// Los recorridos de VdfRecorridoPracticasTests escriben los parámetros y pulsan
        /// START llamando directamente a la simulación: con eso las cinco prácticas salían
        /// en verde mientras, con las gafas puestas, ni el teclado ni los pulsadores
        /// respondían. Este recorre el camino del gatillo: botón del menú, tecla del V1000,
        /// pulsador del tablero y borne, cada uno por su puente.
        /// </summary>
        [UnityTest]
        public IEnumerator EnElVisor_LasPracticasAvanzanPorLosPuentesDelMando()
        {
            if (_xr == null) Assert.Ignore("la escena de VR no tiene VdfXrWiring");
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();

            System.Func<int, VdfXrBotonMenu> botonMenu = indice =>
            {
                foreach (var b in Object.FindObjectsByType<VdfXrBotonMenu>(FindObjectsInactive.Include))
                {
                    var bp = b.GetComponent<VdfBotonPractica>();
                    // Del MENÚ: el panel de diagnóstico usa los mismos índices.
                    if (bp != null && bp.Menu != null && bp.Indice == indice) return b;
                }
                return null;
            };

            // Práctica 1: señalar el breaker por el puente del borne.
            runner.ConfirmarPasoManual();
            foreach (var s in Object.FindObjectsByType<VdfXrSenalarBorne>())
                if (s.Borne != null && s.Borne.Id == "BRK-L1") s.Senalar();
            yield return null;
            Assert.AreEqual(2, runner.PasoActual(), "señalar el breaker con el mando no cumplió el paso");

            // Práctica 2 desde el menú, y una tecla del V1000 que cambie la pantalla.
            botonMenu(1).Pulsar();
            yield return null;
            Assert.AreEqual(2, runner.Practica.Numero, "el botón del menú no cambió de práctica");

            var vista = Object.FindAnyObjectByType<VfdKeypadView>();
            VdfXrKeyPoke arriba = null;
            foreach (var k in Object.FindObjectsByType<VdfXrKeyPoke>()) if (k.name == "Tecla_Arriba") arriba = k;
            Assert.IsNotNull(arriba, "la tecla ▲ no tiene puente");
            string antes = vista.Teclado.TextoPantalla;
            arriba.Pulsar();
            yield return null;
            Assert.AreNotEqual(antes, vista.Teclado.TextoPantalla, "pulsar ▲ con el mando no hizo nada");

            // Práctica 4: START y SUBIR por los pulsadores del tablero.
            botonMenu(3).Pulsar();
            yield return null;
            yield return null;
            var p = new Dictionary<string, VdfXrPulsador>();
            foreach (var x in Object.FindObjectsByType<VdfXrPulsador>()) p[x.name] = x;
            p["Pulsador_START"].Pulsar();
            p["Pulsador_SUBIR"].Pulsar();

            float t = 0f;
            while (_sim.Plc.NivelActivo < 1 && t < PlcFanLogic.RetardoCambioNivelSegundos + 2f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(1, _sim.Plc.NivelActivo, "START y SUBIR con el mando no arrancaron el motor");
            // Paso 2 = START cumplido. El 3 espera a que el motor ALCANCE los 10 Hz, y con la
            // rampa de aceleración eso tarda más que este test.
            Assert.GreaterOrEqual(runner.PasoActual(), 2, "la práctica 4 no avanzó con los pulsadores");
        }

        [UnityTest]
        public IEnumerator LaEtiquetaDeUnaTecla_NoTapaNiLaPantallaNiOtrasTeclas()
        {
            var teclas = Object.FindObjectsByType<VdfXrKeyPoke>();
            Assert.Greater(teclas.Length, 0, "no hay teclas con puente");
            // Se compara lo que VE la cámara, no alturas del mundo: la etiqueta se acerca al ojo
            // para que no la tape el variador, y vista desde arriba eso la sube en el mundo
            // aunque en la imagen quede en el mismo sitio.
            var cam = Camera.main;
            Assert.IsNotNull(cam, "no hay cámara principal");
            float filaMasBaja = float.MaxValue;
            foreach (var k in teclas)
                filaMasBaja = Mathf.Min(filaMasBaja, cam.WorldToViewportPoint(k.transform.position + Vector3.down * 0.004f).y);

            foreach (var k in teclas)
            {
                k.Apuntar();
                yield return null;
                string texto;
                Assert.IsTrue(VdfEtiquetaFlotante.Visible(out texto), k.name + ": no salió la etiqueta");
                var b = VdfEtiquetaFlotante.Limites();
                float arriba = cam.WorldToViewportPoint(new Vector3(b.center.x, b.max.y, b.center.z)).y;
                Assert.Less(arriba, filaMasBaja,
                    k.name + ": en la imagen la etiqueta sube hasta " + arriba.ToString("F3") + " y tapa el teclado o la pantalla (fila baja en " + filaMasBaja.ToString("F3") + ")");
                // 0,8°: la medida toma la primera mayúscula, que en las flechas es la de la
                // explicación (al 80 %).
                Assert.GreaterOrEqual(VdfEtiquetaFlotante.AlturaAngular(), 0.8f, k.name + ": la etiqueta sale demasiado pequeña");
                k.DejarDeApuntar();
                yield return null;
            }
        }

        /// <summary>
        /// Lo que hay que señalar lleva un aro magenta además del cambio de color del borne,
        /// que en amarillo «apenas se ve».
        /// </summary>
        [UnityTest]
        public IEnumerator EnElReconocimiento_ElObjetivoLlevaUnAro()
        {
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            var pista = Object.FindAnyObjectByType<VdfPistaObjetivo>();
            Assert.IsNotNull(pista, "no hay pista en la escena");
            runner.Empezar(VdfPracticasDefaults.CrearReconocimiento());
            runner.ConfirmarPasoManual();   // el primer paso es de leer; el segundo, señalar F1

            int maximo = 0;
            for (int i = 0; i < 90; i++) { maximo = Mathf.Max(maximo, pista.ArosVisibles); yield return null; }
            Assert.Greater(maximo, 0, "el borne a señalar no tiene aro");
        }

        [UnityTest]
        public IEnumerator EnElReconocimiento_PulsarStartSoloLoSenala()
        {
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            runner.Empezar(VdfPracticasDefaults.CrearReconocimiento());
            yield return null;

            VdfPulsadorTablero start = null;
            foreach (var p in Object.FindObjectsByType<VdfPulsadorTablero>())
                if (p.Accion == VdfAccionPulsador.Start) start = p;
            Assert.IsNotNull(start, "no hay pulsador START");
            start.Pulsar();
            yield return null;

            Assert.IsFalse(_sim.Plc.Habilitado, "en el reconocimiento START no debería habilitar el PLC");
        }

        /// <summary>
        /// En la práctica 2, RUN en LOCAL no arranca el motor y la guía dice por qué. Al
        /// pasar a la 4, el motor arranca con normalidad.
        /// </summary>
        [UnityTest]
        public IEnumerator EnLaPractica2_RunNoArrancaElMotorYLaGuiaLoExplica()
        {
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            runner.Empezar(VdfPracticasDefaults.CrearConfiguracion());
            yield return null;

            var vista = Object.FindAnyObjectByType<VfdKeypadView>();
            vista.Pulsar(VfdKey.LoRe);
            vista.Pulsar(VfdKey.Run);
            for (int i = 0; i < 60; i++) yield return null;

            Assert.Less(_sim.Vfd.FrecuenciaSalidaHz, 0.01f, "el motor arrancó en la práctica de configuración");
            Assert.AreEqual(VdfPracticaRunner.AvisoMarchaBloqueada, runner.DetalleDelPasoActual(), "la guía no explica por qué no arranca");

            runner.Empezar(VdfPracticasDefaults.CrearOperacion());
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsFalse(_sim.Vfd.MarchaBloqueada, "en la práctica 4 el motor tiene que poder arrancar");
            Assert.Less(_sim.Vfd.FrecuenciaSalidaHz, 0.01f, "el RUN de la práctica 2 se quedó pendiente y arrancó en la 4");
        }

        /// <summary>
        /// Al apuntar un borne, su nombre aparece flotando en grande. Los rótulos del tablero
        /// se ven a 0,2° desde el puesto: con las gafas no se leen.
        /// </summary>
        [UnityTest]
        public IEnumerator AlApuntarUnBorne_SuNombreSaleEnGrande()
        {
            VdfXrSenalarBorne l1 = null;
            foreach (var s in Object.FindObjectsByType<VdfXrSenalarBorne>())
                if (s.Borne != null && s.Borne.Id == "BRK-L1") l1 = s;
            Assert.IsNotNull(l1);

            l1.Apuntar();
            yield return null;
            string texto;
            Assert.IsTrue(VdfEtiquetaFlotante.Visible(out texto), "al apuntar no salió ninguna etiqueta");
            Assert.AreEqual("F1  (breaker)", texto);

            Assert.GreaterOrEqual(VdfEtiquetaFlotante.AlturaAngular(), 1.0f, "la etiqueta flotante sale demasiado pequeña");

            l1.DejarDeApuntar();
            yield return null;
            Assert.IsFalse(VdfEtiquetaFlotante.Visible(out texto), "la etiqueta no se fue al dejar de apuntar");
        }

        [UnityTest]
        public IEnumerator LaGuiaYElMenu_SeLeenDesdeLosPuestos()
        {
            yield return null;
            var pequenos = new List<string>();
            foreach (var raiz in new[] { VdfPanelGuia.NombreRaiz, VdfMenuPracticas.NombreRaiz, VdfPanelDiagnostico.NombreRaiz })
                pequenos.AddRange(VdfAuditoriaXr.TextosPequenos(raiz, 0.75f));
            Assert.IsEmpty(pequenos, "textos por debajo del mínimo legible: " + string.Join(", ", pequenos.ToArray()));
        }

        [UnityTest]
        public IEnumerator AlVolverALaPractica1_NoQuedaNingunaPuntaClavada()
        {
            if (_xr == null) Assert.Ignore("la escena de VR no tiene VdfXrWiring");
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            var menu = Object.FindAnyObjectByType<VdfMenuPracticas>();

            runner.Empezar(menu.Practicas[3]);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(_sim.Cables.Validar().EsCorrecto, "la práctica 4 empieza cableada");

            runner.Empezar(menu.Practicas[0]);
            yield return new WaitForSeconds(0.3f);

            int clavadas = 0, fueraDeSitio = 0;
            foreach (var p in Object.FindObjectsByType<VdfXrPlug>())
            {
                if (VdfXrSetup.EstaEnUnSocket(p.gameObject)) clavadas++;
                if (Vector3.Distance(p.transform.position, p.Reposo) > 0.02f) fueraDeSitio++;
            }
            Assert.AreEqual(0, clavadas, "quedan puntas sujetas por un socket");
            Assert.AreEqual(0, fueraDeSitio, "quedan puntas fuera de la bandeja");
        }

        [UnityTest]
        public IEnumerator ElTeletransporte_NoLlegaDebajoDeLaMesa()
        {
            yield return null;
            var origen = new Vector3(0f, 1.2f, -0.9f);
            var bajoLaMesa = new Vector3(0.1f, 0.02f, -0.2f);
            RaycastHit hit;
            Assert.IsTrue(Physics.Raycast(origen, (bajoLaMesa - origen).normalized, out hit, 3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
            Assert.AreNotEqual("Suelo", hit.collider.name,
                "el rayo de teletransporte llega al suelo de debajo de la mesa");
        }

        [UnityTest]
        public IEnumerator NingunJoystick_DeslizaAlJugador()
        {
            yield return null;
            Assert.IsFalse(VdfXrLocomocion.HayMovimientoContinuo(),
                "un joystick mueve al jugador de forma continua: marea y no es lo que "
                + "explica la bienvenida");
        }

        [UnityTest]
        public IEnumerator LaPistaDelBorneBuscado_EstaEnLaEscena()
        {
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<VdfPistaObjetivo>(),
                "no hay VdfPistaObjetivo en la escena: quien se atasca no recibe ayuda");
        }

        private static string DescribirCollider(VdfXrPlug punta)
        {
            var c = punta.GetComponent<SphereCollider>();
            if (c == null) return "ninguno";
            return "enabled=" + c.enabled + " capa=" + LayerMask.LayerToName(punta.gameObject.layer) +
                   " activo=" + punta.gameObject.activeInHierarchy + " trigger=" + c.isTrigger;
        }

        [UnityTest]
        public IEnumerator UnaPuntaEnchufadaEnElPlc_SeAlcanzaAntesQueElBorne()
        {
            _xr.SoltarTodos();
            yield return null;
            foreach (var id in new[] { "PLC-1L", "PLC-2L", "S1" })
            {
                VdfXrSocket socket = null;
                foreach (var s in Object.FindObjectsByType<VdfXrSocket>())
                    if (s.Borne != null && s.Borne.Id == id) socket = s;
                Assert.IsNotNull(socket, "no hay socket en " + id);

                VdfXrPlug punta = null;
                foreach (var p in Object.FindObjectsByType<VdfXrPlug>())
                    if (p.Conectado == null) { punta = p; break; }
                Assert.IsNotNull(punta, "no queda ninguna punta libre");

                socket.Recibir(punta);
                yield return null;
                yield return null;
                Assert.AreEqual(socket.Borne, punta.Conectado, "la punta no quedó enchufada en " + id);

                // El rayo, desde delante del borne, tiene que dar con la punta. Antes, que la
                // física se entere de dónde está: la punta se movió por transform y sin un paso
                // de física el rayo la busca todavía en la bandeja.
                Physics.SyncTransforms();
                Vector3 frente = socket.Borne.transform.position - socket.Borne.transform.forward * 0.3f;
                Vector3 dir = (punta.transform.position - frente).normalized;
                RaycastHit hit;
                Assert.IsTrue(Physics.Raycast(frente, dir, out hit, 1f, ~0, QueryTriggerInteraction.Ignore),
                    "el rayo no da con nada en " + id);
                Assert.IsNotNull(hit.collider.GetComponentInParent<VdfXrPlug>(),
                    "en " + id + " el rayo da con " + hit.collider.name + " a " + hit.distance.ToString("F4") +
                    " m y no con la punta enchufada, a " + Vector3.Distance(frente, punta.transform.position).ToString("F4") +
                    " m (collider de la punta: " + DescribirCollider(punta) + ")");

                // Y al sacarla, el borne vuelve a ser señalable.
                _xr.SoltarTodos();
                yield return null;
                Assert.IsTrue(socket.Borne.GetComponent<Collider>().enabled,
                    id + " vacío tiene que volver a poder señalarse");
            }
        }

            private static List<VdfXrCable> CablesLibres()
        {
            var libres = new List<VdfXrCable>();
            foreach (var c in Object.FindObjectsByType<VdfXrCable>())
                if (c.ExtremoA != null && c.ExtremoB != null &&
                    c.ExtremoA.Conectado == null && c.ExtremoB.Conectado == null) libres.Add(c);
            return libres;
        }

        private static VdfXrSocket SocketDe(string id)
        {
            foreach (var s in Object.FindObjectsByType<VdfXrSocket>())
                if (s.Borne != null && s.Borne.Id == id) return s;
            return null;
        }

        [UnityTest]
        public IEnumerator DosCablesEnElMismoBorne_SeApilanYUnenLaRed()
        {
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            runner.Empezar(VdfPracticasDefaults.CrearCableado());
            for (int i = 0; i < 10; i++) yield return null;
            _xr.SoltarTodos();
            yield return null;

            var libres = CablesLibres();
            Assert.GreaterOrEqual(libres.Count, 2);
            var uno = libres[0];
            var dos = libres[1];

            Assert.IsTrue(SocketDe("BRK-L1").Recibir(uno.ExtremoA));
            Assert.IsTrue(SocketDe("R/L1").Recibir(uno.ExtremoB));
            yield return null;

            // La segunda punta se suelta encima de R/L1, que ya está ocupado.
            dos.ExtremoA.transform.position = SocketDe("R/L1").transform.position;
            dos.ExtremoA.GetComponent<VdfEncajeAlSoltar>().Encajar();
            Assert.IsTrue(SocketDe("PLC-1L").Recibir(dos.ExtremoB));
            yield return null;

            var rl1 = SocketDe("R/L1").Borne;
            Assert.AreEqual(rl1, dos.ExtremoA.Conectado, "la segunda punta no quedó en R/L1");
            Assert.AreEqual(2, rl1.Enchufados, "R/L1 tiene que contar las dos puntas");
            Assert.IsTrue(_xr.Grafo.MismaRed("BRK-L1", "PLC-1L"),
                "con el cable apilado, el breaker tiene que llegar al común 1L del PLC");

            _xr.SoltarTodos();
            yield return null;
            Assert.AreEqual(0, rl1.Enchufados, "al soltarlo todo, R/L1 tiene que quedar vacío");
        }

        [UnityTest]
        public IEnumerator EnReconocimiento_NoSeVenLosCables()
        {
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            runner.Empezar(VdfPracticasDefaults.CrearReconocimiento());
            yield return null;

            Assert.IsFalse(_xr.CablesVisibles);
            foreach (var p in Object.FindObjectsByType<VdfXrPlug>())
            {
                foreach (var r in p.GetComponentsInChildren<Renderer>())
                    Assert.IsFalse(r.enabled, "en reconocimiento se ve la punta " + p.name);
                foreach (var c in p.GetComponentsInChildren<Collider>())
                    Assert.IsFalse(c.enabled, "la punta invisible " + p.name + " sigue tapando el rayo");
            }

            runner.Empezar(VdfPracticasDefaults.CrearCableado());
            yield return null;
            Assert.IsTrue(_xr.CablesVisibles);
            foreach (var p in Object.FindObjectsByType<VdfXrPlug>())
                foreach (var r in p.GetComponentsInChildren<Renderer>())
                    Assert.IsTrue(r.enabled, "en la de cablear no se ve la punta " + p.name);
        }

        /// <summary>
        /// Al abrir la app, que arranca en la práctica 1, los cables ya están ocultos sin que
        /// nadie elija nada. El runner pide ocultarlos en su Awake, que puede correr antes que
        /// el del puente, cuando su lista de cables aún está vacía.
        /// </summary>
        [UnityTest]
        public IEnumerator AlAbrirLaApp_LosCablesYaEstanOcultos()
        {
            // El SetUp los muestra para las demás pruebas: se vuelve a cargar la escena limpia.
            VdfSimulacion.ModoMandoForzado = VdfModoMando.Pulsadores; // estos tests recorren la logica de pulsadores
            SceneManager.LoadScene(EscenaVr, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var xr = Object.FindAnyObjectByType<VdfXrWiring>();
            Assert.IsFalse(xr.CablesVisibles, "la escena no abrió en una práctica sin cables");
            int vistas = 0;
            foreach (var p in Object.FindObjectsByType<VdfXrPlug>())
                foreach (var r in p.GetComponentsInChildren<Renderer>())
                    if (r.enabled) vistas++;
            Assert.AreEqual(0, vistas, "al abrir la app se ven puntas de cable en la práctica 1");
        }

        [UnityTest]
        public IEnumerator DeUnaPracticaCableadaALaDeCablear_NoVuelveNingunaPuntaAlTablero()
        {
            _xr.EnchufarCableadoCorrecto();
            // Que los sockets se queden con sus puntas, como pasa en el visor.
            yield return new WaitForSecondsRealtime(VdfXrWiring.PausaSockets + 0.3f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            _xr.SoltarTodos();
            yield return new WaitForSecondsRealtime(VdfXrWiring.PausaSockets + 0.3f);
            yield return new WaitForFixedUpdate();
            yield return null;

            var enganchadas = new System.Text.StringBuilder();
            foreach (var p in Object.FindObjectsByType<VdfXrPlug>())
                if (p.Conectado != null) enganchadas.Append(p.Conectado.Id).Append(' ');
            Assert.AreEqual("", enganchadas.ToString(),
                "estas puntas volvieron solas a su borne tras soltarlo todo");
            Assert.IsFalse(_xr.SocketsEnPausa, "los bornes tienen que volver a aceptar puntas");
        }

        [UnityTest]
        public IEnumerator LaAveriaDeCable_DejaElCableSueltoALaVista()
        {
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            runner.FallaForzada = "V3_SIN_CABLE";
            runner.Empezar(VdfPracticasDefaults.CrearSeguridadYFallas());
            // Que el cableado automático haya terminado: en la práctica la avería llega en el
            // paso 7, mucho después.
            for (int i = 0; i < 300 && !_xr.Grafo.MismaRed("PLC-Q0.2", "V3-A"); i++) yield return null;
            Assert.IsTrue(_xr.Grafo.MismaRed("PLC-Q0.2", "V3-A"), "la práctica 5 no empezó cableada");
            runner.ProvocarAveriaAhora();
            yield return null;

            Assert.IsFalse(_xr.Grafo.MismaRed("PLC-Q0.2", "V3-A"), "la avería no quitó la unión");

            VdfXrCable suelto = null;
            foreach (var c in Object.FindObjectsByType<VdfXrCable>())
            {
                var a = c.ExtremoA != null ? c.ExtremoA.Conectado : null;
                var b = c.ExtremoB != null ? c.ExtremoB.Conectado : null;
                if ((a != null && a.Id == "PLC-Q0.2" && b == null) || (b != null && b.Id == "PLC-Q0.2" && a == null))
                    suelto = c;
            }
            Assert.IsNotNull(suelto, "el cable de V3 sigue enchufado por los dos lados: la avería no se ve");

            // Y volver a enchufarlo repara la avería.
            var punta = suelto.ExtremoA.Conectado == null ? suelto.ExtremoA : suelto.ExtremoB;
            Assert.IsTrue(SocketDe("V3-A").Recibir(punta));
            yield return null;
            Assert.IsTrue(_xr.Grafo.MismaRed("PLC-Q0.2", "V3-A"), "volver a enchufarlo no reparó la unión");
            runner.FallaForzada = null;
        }

        [UnityTest]
        public IEnumerator LaPuntaSueltaDeSc_QuedaEncimaDeLaMesa()
        {
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            runner.FallaForzada = "SC_SIN_COMUN";
            runner.Empezar(VdfPracticasDefaults.CrearSeguridadYFallas());
            for (int i = 0; i < 300 && !_xr.Grafo.MismaRed("GND24", "SC"); i++) yield return null;
            Assert.IsTrue(_xr.Grafo.MismaRed("GND24", "SC"), "la práctica 5 no empezó cableada");
            runner.ProvocarAveriaAhora();
            yield return null;

            VdfXrPlug suelta = null;
            foreach (var c in Object.FindObjectsByType<VdfXrCable>())
            {
                var a = c.ExtremoA != null ? c.ExtremoA.Conectado : null;
                var b = c.ExtremoB != null ? c.ExtremoB.Conectado : null;
                if (a != null && a.Id == "GND24" && b == null) suelta = c.ExtremoB;
                if (b != null && b.Id == "GND24" && a == null) suelta = c.ExtremoA;
            }
            runner.FallaForzada = null;
            Assert.IsNotNull(suelta, "la avería no dejó suelto el cable de SC");
            Assert.GreaterOrEqual(suelta.transform.position.y, VdfReconstructorEscena.AlturaEncimera + 0.02f,
                "la punta suelta quedó dentro de la mesa, a y = " + suelta.transform.position.y.ToString("F3"));
        }

        /// <summary>
        /// En ningún paso de ninguna práctica el texto de la guía se sale de su caja. En el
        /// paso 9 de la práctica 5 se salía y pisaba el pie: «aquí hay un problema con el
        /// texto».
        /// </summary>
        [UnityTest]
        public IEnumerator EnNingunPaso_ElTextoDeLaGuiaSeSaleDeSuCaja()
        {
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            var guia = Object.FindAnyObjectByType<VdfPanelGuia>();
            Assert.IsNotNull(guia);

            var fallos = new System.Text.StringBuilder();
            foreach (var practica in VdfPracticasDefaults.CrearTodas())
            {
                // Todos los pasos a mano, para poder recorrerlos sin hacerlos.
                foreach (var paso in practica.Pasos) paso.Condicion = VdfCondicion.Manual;
                runner.Empezar(practica);
                for (int i = 0; i < practica.TotalPasos; i++)
                {
                    yield return null;
                    guia.Refrescar();
                    var medidas = guia.AltoCuerpoYCaja();
                    if (medidas.x > medidas.y + 1e-4f)
                        fallos.Append("práctica " + practica.Numero + ", paso " + (i + 1) + ": " +
                                      medidas.x.ToString("F3") + " > " + medidas.y.ToString("F3") + "; ");
                    runner.ConfirmarPasoManual();
                }
            }
            Assert.IsEmpty(fallos.ToString(), "el texto de la guía se sale de su caja");
        }

        /// <summary>
        /// La explicación de cada avería cabe entera en el panel «¿Cuál es la causa?». Con la
        /// caja de siete líneas se cortaban todas, la de b1-01 casi a la mitad.
        /// </summary>
        [UnityTest]
        public IEnumerator LaExplicacionDeCadaAveria_CabeEnElPanel()
        {
            yield return null;
            var panel = Object.FindAnyObjectByType<VdfPanelDiagnostico>();
            Assert.IsNotNull(panel, "no hay panel de diagnóstico");
            var fallos = new System.Text.StringBuilder();
            foreach (var f in VdfCatalogoFallas.Crear())
            {
                var m = panel.AltoResultadoYCaja(VdfPanelDiagnostico.ExplicacionParaElPanel(f.Explicacion));
                if (m.x > m.y + 1e-4f) fallos.Append(f.Id + ": " + m.x.ToString("F3") + " > " + m.y.ToString("F3") + "; ");
            }
            Assert.IsEmpty(fallos.ToString(), "explicaciones que no caben");
        }
    }
}
