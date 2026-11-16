using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VDF.Tests
{
    /// <summary>
    /// Ataques deliberados contra el simulador: el alumno torpe, caótico y malicioso.
    ///
    /// Por qué existe. Los 295 tests que ya hay comprueban el camino feliz
    /// (<c>VdfRecorridoPracticasTests</c>), el camino torcido razonable
    /// (<c>VdfFueraDeGuionTests</c>) y el cableado en VR (<c>VdfEscenaVrTests</c>). Todos
    /// comparten un supuesto: que quien usa el módulo lo usa a una velocidad humana y en un
    /// orden más o menos sensato.
    ///
    /// Este archivo rompe ese supuesto a propósito. Aquí se tiran cien cables donde caben
    /// diecinueve, se enchufa y desenchufa el mismo par doscientas veces, se arranca una
    /// práctica cincuenta veces seguidas en el mismo fotograma, se aporrea el teclado del
    /// variador con dos mil pulsaciones al azar, se dejan parámetros a medio escribir y se
    /// tocan botones que el guion no menciona.
    ///
    /// Las dos preguntas que contesta cada ataque son:
    ///
    ///   1. **¿Se traba o revienta?** Una excepción en el bucle de la simulación deja la
    ///      aplicación muerta hasta reiniciarla, y en Quest eso es cerrar el APK.
    ///   2. **¿Fuga objetos?** Cada cable son cinco GameObjects. Si al borrarlos no se
    ///      destruyen, veinte minutos de práctica llenan la memoria de la Quest. Por eso
    ///      varios tests cuentan objetos antes y después: una fuga es un defecto aunque no
    ///      se vea en pantalla.
    ///
    /// Los tests marcados con <c>DEFECTO ENCONTRADO</c> están en ROJO a propósito: señalan
    /// algo que de verdad le pasa al alumno y no se tapan para que la tanda quede verde.
    /// Los de la última región son huecos de validación de la API que NO se alcanzan desde
    /// la interfaz; están separados justamente para poder triarlos aparte.
    /// </summary>
    public class VdfUsoAbusivoTests
    {
        private const string Escena = "VDF_Desktop";

        private VdfSimulacion _sim;
        private VdfPracticaRunner _runner;
        private VdfJumperController _cableado;

        [UnitySetUp]
        public IEnumerator Preparar()
        {
            VdfSimulacion.ModoMandoForzado = VdfModoMando.Pulsadores; // estos tests recorren la logica de pulsadores
            SceneManager.LoadScene(Escena, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.25f);

            _sim = Object.FindAnyObjectByType<VdfSimulacion>();
            _runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            _cableado = Object.FindAnyObjectByType<VdfJumperController>();

            Assert.IsNotNull(_sim, "la escena no tiene VdfSimulacion");

            // Estos tests maltratan el MÓDULO, no una práctica. La escena arranca con la
            // práctica 1, que no deja arrancar el motor (VdfPractica.BloqueaMarcha), así que
            // se quita: el módulo queda libre, como en un banco de pruebas.
            if (_runner != null) _runner.Practica = null;
            _sim.Despertar();
            _sim.PulsarStop();
            yield return null;
        }

        // =====================================================================================
        // 1. Cables: la sospecha explícita del usuario — «si me pongo a sacar cables se traba»
        // =====================================================================================

        /// <summary>
        /// Tender CIEN cables donde el esquema pide diecinueve, y después borrarlos todos.
        ///
        /// Es lo que hace quien descubre que puede unir cualquier borne con cualquiera: en
        /// vez de seguir el esquema, llena el tablero. Cada cable son cinco GameObjects (el
        /// cable y sus cuatro cápsulas de selección) más una simulación de Verlet propia,
        /// así que si al borrarlos quedara alguno vivo, la fuga se nota enseguida.
        ///
        /// Lo que se exige: que la escena vuelva EXACTAMENTE a como estaba. Un objeto que
        /// sobrevive al borrado es un cable invisible que sigue consumiendo FixedUpdate.
        /// </summary>
        [UnityTest]
        public IEnumerator TenderCienCablesYBorrarlos_NoDejaNiUnObjetoVivo()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            _cableado.IndexarBornes();
            _cableado.BorrarTodo();
            yield return null;
            yield return null;

            int objetosBase = ContarObjetos();
            int cablesBase = ContarCables();
            Assert.AreEqual(0, cablesBase, "el borrado previo debería dejar cero cables");

            // Una cadena de cien cables por bornes que existen de verdad. No tiene ningún
            // sentido eléctrico, y eso es justo lo que se quiere: el alumno tampoco lo tiene.
            // El tablero tiene 79 bornes, asi que «cien cables» no sale de una cadena simple:
            // se recorre la lista varias veces uniendo pares cada vez mas separados. Da el
            // mismo nido de cables sin sentido, que es el objetivo, y no depende de cuantos
            // bornes tenga el tablero el dia que se lea esto.
            var ids = IdsDeBornes();
            Assert.Greater(ids.Count, 10, "sin bornes no hay ataque que hacer");

            int puestos = 0;
            for (int salto = 1; salto < ids.Count && puestos < 100; salto++)
                for (int i = 0; i + salto < ids.Count && puestos < 100; i++)
                    if (_cableado.Conectar(ids[i], ids[i + salto])) puestos++;

            yield return null;
            Assert.GreaterOrEqual(puestos, 50,
                "debería haber podido tender al menos cincuenta cables disparatados");
            Assert.AreEqual(puestos, ContarCables(),
                "los cables dibujados deberían coincidir con los que se dieron por puestos");

            // Y la validación tiene que seguir funcionando con el tablero hecho un nido.
            var informe = _sim.Cables.Validar();
            Assert.IsNotNull(informe, "con cien cables sobrantes el informe debería existir igual");
            Assert.IsFalse(informe.EsCorrecto, "cien cables al azar no pueden dar correcto");

            // Ahora se borra todo, que es la parte que de verdad se está probando.
            _cableado.BorrarTodo();
            yield return null;
            yield return null;

            Assert.AreEqual(0, ContarCables(), "tras borrar no debería quedar ningún cable");
            // Lo que sobra son anclajes, y es ACOTADO, no una fuga.
            //
            // Este ataque encontro 46 objetos de mas tras borrar los cien cables, que era la
            // sospecha de partida: que sacar cables fuera dejando basura hasta trabar la
            // simulacion. Mirado de cerca, no lo es. VdfCableSpawner.AnclajeDe cuelga de cada
            // borne un objeto vacio «AnclajeCable» del que tira el cable, y lo REUTILIZA: el
            // primer cable que toca un borne lo crea y todos los demas lo encuentran hecho.
            // Asi que el maximo posible es uno por borne, 79 en este tablero, y ahi se queda
            // por muchos cables que se tiendan y se borren.
            //
            // La prueba de que no crece esta en el ataque de al lado: enchufar y desenchufar
            // doscientas veces no deja ni un objeto de mas. Si los anclajes se duplicaran,
            // ese habria explotado el primero.
            //
            // Por eso aqui se mide lo que importa de verdad —que lo que sobra son anclajes y
            // que no pasan de uno por borne— en vez de exigir una vuelta exacta al punto de
            // partida, que obligaria a destruir y recrear los anclajes a cada cable sin ganar
            // nada.
            int sobran = ContarObjetos() - objetosBase;
            int bornes = IdsDeBornes().Count;

            Assert.LessOrEqual(sobran, bornes,
                "sobran " + sobran + " objetos para " + bornes + " bornes: si pasa de uno por "
                + "borne ya no son anclajes reutilizados, es una fuga de verdad");

            Assert.AreEqual(0, ContarCables(),
                "de cables no puede quedar ninguno vivo tras borrarlos todos");
        }

        /// <summary>
        /// Enchufar y desenchufar el MISMO par doscientas veces seguidas.
        ///
        /// Es el tic nervioso de quien no está seguro: pone el cable, lo quita, lo vuelve a
        /// poner. Cada <c>Conectar</c> crea el cable y lo deja caer con 420 pasos de
        /// solucionador, y cada <c>BorrarCablesDe</c> lo destruye; si algo de eso acumulara
        /// —un anclaje por cada enchufado, una cápsula huérfana, un material nuevo— saldría
        /// aquí multiplicado por doscientos.
        /// </summary>
        [UnityTest]
        public IEnumerator EnchufarYDesenchufarDoscientasVeces_NiFugaNiAnclajesDuplicados()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            _cableado.IndexarBornes();
            _cableado.BorrarTodo();

            // Una vuelta de calentamiento ANTES de contar. Cada borne crea su punto de
            // anclaje la primera vez que recibe un cable y luego lo reutiliza. Desde que la
            // app arranca en la práctica 1 sin cables, esos dos anclajes aún no existen al
            // empezar, y el test los contaba como fuga: «sobran 2».
            _cableado.Conectar("PLC-Q0.7", "S1");
            _cableado.BorrarCablesDe("S1");
            yield return null;
            yield return null;

            int objetosBase = ContarObjetos();

            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < 200; i++)
            {
                Assert.IsTrue(_cableado.Conectar("PLC-Q0.7", "S1"),
                    "en la vuelta " + i + " ya no se puede volver a conectar el mismo par: "
                    + "el grafo se quedó con una arista fantasma");
                Assert.AreEqual(1, _cableado.BorrarCablesDe("S1"),
                    "en la vuelta " + i + " el borrado no encontró el cable que se acaba de poner");
            }
            float ms = (Time.realtimeSinceStartup - t0) * 1000f;
            Debug.Log("[ATAQUE] 200 ciclos enchufar/desenchufar: " + ms.ToString("0") + " ms ("
                      + (ms / 200f).ToString("0.0") + " ms por cable)");

            yield return null;
            yield return null;

            Assert.AreEqual(0, ContarCables(), "no debería quedar ningún cable");
            Assert.AreEqual(objetosBase, ContarObjetos(),
                "doscientos ciclos no deberían dejar objetos de más. Sobran "
                + (ContarObjetos() - objetosBase));

            // Los puntos de anclaje se crean bajo el borne la primera vez y se reutilizan.
            // Si se crearan uno por cable, cada borne acabaría con doscientos hijos.
            var bornes = Object.FindObjectsByType<VdfTerminal>();
            for (int i = 0; i < bornes.Length; i++)
            {
                int anclajes = 0;
                foreach (Transform hijo in bornes[i].transform)
                    if (hijo.name == "AnclajeCable") anclajes++;

                Assert.LessOrEqual(anclajes, 1,
                    "el borne " + bornes[i].Id + " acumuló " + anclajes + " anclajes de cable");
            }
        }

        /// <summary>
        /// Sacar el cable del motor MIENTRAS el motor gira.
        ///
        /// Es el ataque que describió el usuario con sus palabras. En el módulo real tirar de
        /// una fase con el motor en marcha es exactamente lo que no hay que hacer, así que el
        /// simulador tiene que tener una respuesta: el eje se para, y nada se traba.
        /// </summary>
        [UnityTest]
        public IEnumerator SacarElCableDelMotorConElMotorGirando_ParaElEjeSinTrabarse()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            yield return Cablear();
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);
            yield return new WaitForSeconds(1.5f);

            var eje = BuscarEje();
            Assert.IsNotNull(eje, "no se encontró el eje del motor en la escena");
            Assert.IsTrue(_sim.Puerta.Motor, "con todo cableado el motor debería tener sus tres fases");
            Assert.Greater(_sim.Vfd.FrecuenciaSalidaHz, 0f, "el motor debería estar girando. " + _sim.Resumen());

            // Y de un tirón, fuera la fase U.
            _cableado.BorrarCablesDe("MOT-U");
            _cableado.AvisarCableadoCambiado();
            _sim.RevisarCableado();
            yield return null;

            Assert.IsFalse(_sim.Puerta.Motor,
                "sin la fase U el motor no puede seguir alimentado. " + _sim.Resumen());

            Quaternion antes = eje.localRotation;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, Quaternion.Angle(antes, eje.localRotation), 0.01f,
                "con una fase fuera el eje no debería seguir girando");

            // Y volver a enchufarla lo arregla, sin reiniciar nada.
            yield return Cablear();
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(_sim.Puerta.Motor, "al devolver la fase el motor debería volver a tener alimentación");
        }

        /// <summary>
        /// Recablear entero el módulo justo en mitad del retardo de 3 s.
        ///
        /// El caso feo: el PLC tiene un cambio de nivel en vuelo y el alumno, a la vez,
        /// arranca el cableado de cuajo y lo vuelve a poner. El cambio de nivel no puede
        /// quedarse colgado para siempre ni saltarse el retardo.
        /// </summary>
        [UnityTest]
        public IEnumerator RecablearEnMitadDelRetardo_NoCongelaElCambioDeNivel()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            yield return Cablear();
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);

            _sim.PulsarSubir();                  // pide la 2: arranca el retardo
            yield return new WaitForSeconds(1f); // justo en medio

            _cableado.BorrarTodo();
            _cableado.AvisarCableadoCambiado();
            _sim.RevisarCableado();
            yield return null;
            yield return Cablear();

            yield return new WaitForSeconds(PlcFanLogic.RetardoCambioNivelSegundos + 1.5f);

            Assert.AreEqual(2, _sim.Plc.NivelActivo,
                "el cambio a la velocidad 2 debería haberse completado igual: el cableado no "
                + "manda en el temporizador del PLC. " + _sim.Resumen());
            Assert.IsFalse(_sim.Plc.CambioEnCurso, "y sin dejar ningún cambio colgado");
            Assert.Greater(_sim.Vfd.FrecuenciaSalidaHz, 0f,
                "con el cableado restituido el motor debería volver a girar. " + _sim.Resumen());
        }

        /// <summary>
        /// Romper el cableado EN EL INSTANTE exacto en que vence el retardo.
        ///
        /// Se busca la ventana de un fotograma: el PLC acaba de aplicar el nivel nuevo y en
        /// ese mismo frame desaparece el cable de S1. No puede quedar un estado híbrido en el
        /// que el indicador diga una velocidad y el variador otra para siempre.
        /// </summary>
        [UnityTest]
        public IEnumerator RomperElCableadoAlVencerElRetardo_NoDejaEstadoHibrido()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            yield return Cablear();
            _sim.PulsarStart();
            _sim.PulsarSubir();

            // Se espera a que el nivel cambie y se corta en el primer frame posible.
            yield return EsperarNivel(1);
            _cableado.BorrarCablesDe("S1");
            _cableado.AvisarCableadoCambiado();
            _sim.RevisarCableado();
            yield return null;

            Assert.AreEqual(1, _sim.Plc.NivelActivo, "el PLC sigue a lo suyo: él no sabe de cables");
            Assert.IsTrue(_sim.Plc.SalidaS1, "y sigue mandando la orden de marcha");
            Assert.IsFalse(_sim.Puerta.S1, "pero la orden ya no llega al variador");

            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(0f, _sim.Vfd.FrecuenciaSalidaHz, 0.01f,
                "sin la orden en el borne S1 el variador debería frenar hasta cero. " + _sim.Resumen());

            // Y es recuperable sin reiniciar: se vuelve a poner el cable y arranca.
            yield return Cablear();
            yield return new WaitForSeconds(2f);
            Assert.Greater(_sim.Vfd.FrecuenciaSalidaHz, 0f,
                "al devolver el cable de S1 el motor debería volver a arrancar. " + _sim.Resumen());
        }

        // =====================================================================================
        // 2. Fugas y acumulación
        // =====================================================================================

        /// <summary>
        /// Arrancar prácticas veinte veces seguidas, como quien no decide cuál hacer.
        ///
        /// Cada <c>Empezar</c> aplica el punto de partida, y eso borra los diecinueve cables
        /// y los vuelve a tender con física. Dos cosas se miden:
        ///
        ///   - Que la cuenta de objetos no crezca: si cada vuelta dejara un cable sin
        ///     destruir, veinte vueltas serían ciento noventa GameObjects de más.
        ///   - Cuánto cuesta. Se deja escrito en la consola, porque el coste de cada cambio
        ///     de práctica es un parón visible para el alumno y conviene tenerlo medido.
        /// </summary>
        [UnityTest]
        public IEnumerator ArrancarPracticasVeinteVeces_NoAcumulaObjetos()
        {
            if (_runner == null) Assert.Ignore("la escena no tiene VdfPracticaRunner");

            var practicas = VdfPracticasDefaults.CrearTodas();

            // Una vuelta de calentamiento para que la cuenta base ya incluya lo que crea la
            // primera práctica (materiales cacheados, anclajes, etc.).
            _runner.Empezar(practicas[3]);
            yield return null;
            yield return null;

            int objetosBase = ContarObjetos();
            int cablesBase = ContarCables();

            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < 20; i++)
            {
                _runner.Empezar(practicas[i % practicas.Count]);
                yield return null;
            }
            float ms = (Time.realtimeSinceStartup - t0) * 1000f;

            // Se vuelve a la misma práctica del calentamiento para comparar manzanas con
            // manzanas: cada práctica deja un número distinto de cables puestos.
            _runner.Empezar(practicas[3]);
            yield return null;
            yield return null;

            Debug.Log("[ATAQUE] 20 cambios de práctica: " + ms.ToString("0") + " ms ("
                      + (ms / 20f).ToString("0") + " ms por cambio)");

            Assert.AreEqual(cablesBase, ContarCables(),
                "volver a la misma práctica debería dejar los mismos cables, no más");
            Assert.LessOrEqual(ContarObjetos(), objetosBase + 2,
                "veinte cambios de práctica no deberían dejar objetos de más. Sobran "
                + (ContarObjetos() - objetosBase));
        }

        /// <summary>
        /// Pedir la revalidación del cableado mil veces en un solo fotograma.
        ///
        /// Es lo que pasaría con un evento que se dispara en cascada o con un componente que
        /// llama a <c>RevisarCableado</c> desde su Update. No puede reventar ni dejar el
        /// informe en un estado distinto del que daría una sola llamada.
        /// </summary>
        [UnityTest]
        public IEnumerator RevisarElCableadoMilVecesEnUnFrame_NoCambiaElResultado()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            yield return Cablear();
            var antes = _sim.Cables.Validar();
            Assert.IsTrue(antes.EsCorrecto, "el cableado de partida debería estar correcto");

            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < 1000; i++)
            {
                _sim.RevisarCableado();
                _cableado.AvisarCableadoCambiado();
            }
            float ms = (Time.realtimeSinceStartup - t0) * 1000f;
            Debug.Log("[ATAQUE] 1000 revalidaciones en un frame: " + ms.ToString("0") + " ms");

            yield return null;

            Assert.IsNotNull(_sim.InformeCableado, "el informe debería seguir existiendo");
            Assert.IsTrue(_sim.InformeCableado.EsCorrecto,
                "mil revalidaciones no deberían cambiar el veredicto");
            Assert.AreEqual(antes.CablesPuestos, _sim.InformeCableado.CablesPuestos,
                "ni el número de cables contados");
        }

        // =====================================================================================
        // 3. Reentrada y orden invertido
        // =====================================================================================

        /// <summary>
        /// <c>Empezar</c> cinco veces en el mismo fotograma, sin dejar pasar un frame.
        ///
        /// Es el doble clic nervioso en el menú de prácticas. Cada llamada reinicia el
        /// contexto y vuelve a suscribirse a los eventos del cableado; si la suscripción se
        /// duplicara, cada borne señalado se contaría varias veces.
        /// </summary>
        [UnityTest]
        public IEnumerator EmpezarCincoVecesEnElMismoFrame_NoDuplicaNada()
        {
            if (_runner == null) Assert.Ignore("la escena no tiene VdfPracticaRunner");

            var reconocimiento = VdfPracticasDefaults.CrearReconocimiento();
            for (int i = 0; i < 5; i++) _runner.Empezar(reconocimiento);
            yield return null;

            Assert.AreEqual(0, _runner.PasosCumplidos,
                "cinco arranques seguidos no deberían dar ningún paso por hecho");
            Assert.Greater(_runner.TotalPasos, 0, "y la práctica debería tener pasos");

            // Y señalar un borne una sola vez tiene que contar una sola vez. Si la
            // suscripción a BorneSenalado se hubiera duplicado, esto seguiría pareciendo
            // correcto, así que además se comprueba el progreso de grupo, que sí cuenta.
            int antes = _runner.PasosCumplidos;
            var borne = _cableado != null ? _cableado.BuscarBorne("S1") : null;
            if (borne != null)
            {
                _cableado.SenalarBorne(borne);
                yield return null;
                Assert.GreaterOrEqual(_runner.PasosCumplidos, antes,
                    "señalar un borne no puede hacer retroceder el progreso");
            }
        }

        /// <summary>
        /// <c>Despertar</c> repetido y punto de partida aplicado en mitad del retardo.
        ///
        /// Despertar dos veces está documentado como seguro, pero nunca se había probado
        /// combinado con el peor momento posible: con un cambio de nivel en vuelo. Lo que no
        /// puede pasar es que el motor resucite tres segundos después de haber reiniciado.
        /// </summary>
        [UnityTest]
        public IEnumerator DespertarYReiniciarEnMitadDelRetardo_NoResucitaElMotor()
        {
            yield return Cablear();
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);

            _sim.PulsarSubir();                  // retardo en marcha
            yield return new WaitForSeconds(1f);

            var vfdAntes = _sim.Vfd;
            var plcAntes = _sim.Plc;

            _sim.Despertar();
            _sim.Despertar();
            _sim.SaltarConfiguracion = true;
            _sim.SaltarCableado = true;
            _sim.AplicarPuntoDePartida();
            yield return null;

            Assert.AreSame(vfdAntes, _sim.Vfd, "Despertar repetido no debería cambiar el modelo del variador");
            Assert.AreSame(plcAntes, _sim.Plc, "ni el del PLC");
            Assert.AreEqual(0, _sim.Plc.NivelActivo, "el punto de partida debería dejarlo parado");
            Assert.IsFalse(_sim.Plc.CambioEnCurso, "y sin cambio pendiente");

            yield return new WaitForSeconds(PlcFanLogic.RetardoCambioNivelSegundos + 1f);
            Assert.AreEqual(0, _sim.Plc.NivelActivo,
                "el cambio que estaba en vuelo no puede arrancar el motor después de reiniciar. "
                + _sim.Resumen());

            // Y el módulo sigue siendo operable, que es lo que convierte esto en un susto y
            // no en un callejón sin salida.
            yield return Cablear();
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);
            Assert.AreEqual(1, _sim.Plc.NivelActivo, "tras el reinicio debería volver a arrancar. " + _sim.Resumen());
        }

        [UnityTest]
        public IEnumerator HongoYRearmeDoscientasVecesSeguidas_AcabaEnEstadoCoherente()
        {
            yield return Cablear();
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);

            for (int i = 0; i < 200; i++)
            {
                _sim.PulsarEmergencia();
                _sim.Rearmar();
            }
            yield return null;

            Assert.IsFalse(_sim.Plc.EmergenciaActiva, "el último rearme debería haber liberado el hongo");
            Assert.AreNotEqual(VfdFault.ParadaEmergencia, _sim.Vfd.Falla,
                "y el variador no debería quedarse con la falla de emergencia pegada. " + _sim.Resumen());
            Assert.IsFalse(_sim.Plc.Habilitado, "tras un rearme hay que volver a pulsar START");

            // Lo que de verdad importa: que el módulo siga sirviendo.
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);
            Assert.AreEqual(1, _sim.Plc.NivelActivo,
                "después de doscientas emergencias el módulo debería seguir arrancando. " + _sim.Resumen());
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(_sim.Vfd.FrecuenciaSalidaHz, 0f,
                "y el motor debería girar de verdad, no solo el PLC. " + _sim.Resumen());
        }

        [UnityTest]
        public IEnumerator CambiarDePracticaConElHongoPulsado_DejaElModuloSano()
        {
            if (_runner == null) Assert.Ignore("la escena no tiene VdfPracticaRunner");

            yield return Cablear();
            _runner.Empezar(VdfPracticasDefaults.CrearOperacion());
            yield return null;
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);

            _sim.PulsarEmergencia();
            yield return null;
            Assert.IsTrue(_sim.Plc.EmergenciaActiva, "el hongo debería estar enclavado");
            Assert.AreEqual(VfdFault.ParadaEmergencia, _sim.Vfd.Falla, "y el variador en falla");

            // Sin rearmar, directo a otra práctica.
            _runner.Empezar(VdfPracticasDefaults.CrearOperacion());
            yield return null;

            Assert.IsFalse(_sim.Plc.EmergenciaActiva,
                "la práctica nueva no puede heredar el hongo enclavado");
            Assert.AreEqual(VfdFault.Ninguna, _sim.Vfd.Falla,
                "ni la falla del variador: el alumno se encontraría el módulo muerto. " + _sim.Resumen());

            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(_sim.Vfd.FrecuenciaSalidaHz, 0f,
                "y la práctica nueva debería poder hacerse. " + _sim.Resumen());
        }

        // =====================================================================================
        // 4. Teclado del V1000
        // =====================================================================================

        /// <summary>
        /// Dos mil pulsaciones al azar en el teclado del variador.
        ///
        /// Un alumno que no sabe qué hace aporrea: entra y sale de menús a medias, deja
        /// parámetros a medio escribir, pulsa RUN donde no toca. Lo que se exige es que la
        /// pantalla nunca se quede en blanco ni en un texto imposible, que no salte ninguna
        /// excepción, y que el módulo se pueda recuperar aplicando el punto de partida.
        ///
        /// La semilla es fija para que, si algún día falla, falle siempre igual.
        /// </summary>
        [UnityTest]
        public IEnumerator AporrearElTecladoDosMilVeces_NoRompeLaPantalla()
        {
            var operador = _sim.Operador;
            if (operador == null || operador.Teclado == null)
                Assert.Ignore("la escena no tiene el operador digital del variador");

            var teclas = (VfdKey[])System.Enum.GetValues(typeof(VfdKey));
            var dado = new System.Random(20261107);

            for (int i = 0; i < 2000; i++)
            {
                operador.Pulsar(teclas[dado.Next(teclas.Length)]);

                string pantalla = operador.Teclado.TextoPantalla;
                Assert.IsNotNull(pantalla, "la pantalla se quedó en null en la pulsación " + i);
                Assert.IsNotEmpty(pantalla, "la pantalla se quedó vacía en la pulsación " + i);
                Assert.LessOrEqual(pantalla.Length, VfdKeypad.Digitos + 1,
                    "la pantalla del V1000 tiene cinco dígitos y mostró '" + pantalla
                    + "' en la pulsación " + i);

                if ((i % 200) == 0) yield return null;
            }
            yield return null;

            // Y de ese caos se tiene que poder volver.
            _sim.SaltarConfiguracion = true;
            _sim.SaltarCableado = true;
            _sim.AplicarPuntoDePartida();
            yield return Cablear();
            yield return null;

            Assert.IsEmpty(_sim.Vfd.ValidarConfiguracion(),
                "tras aporrear el teclado, el punto de partida debería dejar el variador "
                + "configurado y sin avisos");

            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(_sim.Vfd.FrecuenciaSalidaHz, 0f,
                "y el módulo debería volver a funcionar. " + _sim.Resumen());
        }

        /// <summary>
        /// DEFECTO ENCONTRADO — un parámetro a medio editar sobrevive al cambio de práctica
        /// y se puede guardar después, machacando la configuración recién aplicada.
        ///
        /// El ataque: en la práctica 2 el alumno entra a editar E1-04, deja el valor a medias
        /// (sin pulsar ENTER), se aburre y salta a la práctica 4. Al llegar, el módulo se
        /// reconfigura solo (E1-04 = 60 Hz), pero **el teclado sigue en modo edición con el
        /// valor viejo en los dígitos**. La primera vez que el alumno pulsa ENTER —por
        /// ejemplo para confirmar algo que cree que está mirando— ese valor entra de verdad.
        ///
        /// Síntoma para el alumno: en la práctica de operación, la velocidad 4 deja de dar
        /// 50 Hz y se queda recortada, sin que él haya tocado nada de eso. La guía dice que
        /// la configuración es correcta porque nadie ha vuelto a validarla.
        ///
        /// Lo razonable sería que cambiar de práctica devolviera el teclado al monitor, igual
        /// que devuelve el cableado y el PLC a su punto de partida.
        /// </summary>
        [UnityTest]
        public IEnumerator ParametroAMedioEditar_SobreviveAlCambioDePracticaYSeGuardaDespues()
        {
            var operador = _sim.Operador;
            if (operador == null || operador.Teclado == null)
                Assert.Ignore("la escena no tiene el operador digital del variador");
            if (_runner == null) Assert.Ignore("la escena no tiene VdfPracticaRunner");

            // Práctica 2: el alumno se mete a tocar E1-04.
            _runner.Empezar(VdfPracticasDefaults.CrearConfiguracion());
            yield return null;

            Assert.IsTrue(operador.Teclado.IrAParametro("E1-04"), "debería existir el parámetro E1-04");
            operador.Pulsar(VfdKey.Enter);   // entra a editar
            Assert.AreEqual(VfdKeypadState.Edicion, operador.Teclado.Estado, "debería estar editando");

            // Baja el dígito de las decenas hasta dejar un valor cualquiera, y NO guarda.
            operador.Pulsar(VfdKey.Reset);
            operador.Pulsar(VfdKey.Abajo);
            operador.Pulsar(VfdKey.Abajo);
            yield return null;

            // Y se va a la práctica 4, que reconfigura el módulo entero.
            _runner.Empezar(VdfPracticasDefaults.CrearOperacion());
            yield return null;

            Assert.AreEqual(60f, _sim.Vfd.Valor("E1-04"), 0.05f,
                "la práctica 4 empieza configurada: E1-04 debería valer 60 Hz");

            // DEFECTO ENCONTRADO: el teclado sigue en Edicion, con los dígitos de la práctica
            // anterior. Cambiar de práctica reinicia el PLC, el cableado y los parámetros,
            // pero no el operador digital.
            Assert.AreNotEqual(VfdKeypadState.Edicion, operador.Teclado.Estado,
                "DEFECTO: cambiar de práctica deja el teclado del variador en modo edición con "
                + "el valor a medias de la práctica anterior. Pantalla: '"
                + operador.Teclado.TextoPantalla + "'");

            // Y la consecuencia: un ENTER cualquiera machaca la configuración buena.
            operador.Pulsar(VfdKey.Enter);
            yield return null;
            Assert.AreEqual(60f, _sim.Vfd.Valor("E1-04"), 0.05f,
                "DEFECTO: pulsar ENTER en la práctica 4 guardó el valor que estaba a medio "
                + "editar en la práctica 2. E1-04 quedó en " + _sim.Vfd.Valor("E1-04") + " Hz");
        }

        // =====================================================================================
        // 5. Interacción entre sistemas: botones que el guion no menciona
        // =====================================================================================

        /// <summary>
        /// DEFECTO ENCONTRADO — pulsar LO/RE en el teclado del variador deja el tablero
        /// muerto y el simulador no lo explica por ningún sitio.
        ///
        /// El ataque es de lo más natural: el teclado del V1000 tiene ocho teclas y el alumno
        /// las prueba todas. LO/RE pasa el variador a LOCAL, y en LOCAL la orden de marcha
        /// viene de la tecla RUN y **los terminales S1/S5/S6 dejan de contar**. Es el
        /// comportamiento del equipo real, así que no está mal modelado.
        ///
        /// Lo que está mal es que no hay retroalimentación. Tras pulsarlo:
        ///   - START y SUBIR siguen respondiendo: el PLC sube de nivel y el indicador V1 se
        ///     enciende, o sea que el tablero parece vivo.
        ///   - El motor no gira y nunca girará.
        ///   - <c>ValidarConfiguracion()</c> —que es justo el componente que el simulador usa
        ///     para explicar «por qué no arranca», y la condición del paso «variador
        ///     configurado» de la práctica 2— no dice ni una palabra.
        ///
        /// Síntoma para el alumno: el módulo deja de funcionar de golpe, con los indicadores
        /// encendidos, y la única pista es un LED pequeño en el operador. La reacción normal
        /// es pensar que el simulador está roto. Encima, la práctica 2 se puede dar por
        /// terminada en ese estado, porque la validación la considera correcta.
        /// </summary>
        [UnityTest]
        public IEnumerator PulsarLoReEnElTeclado_DejaElTableroMuertoSinNingunAviso()
        {
            var operador = _sim.Operador;
            if (operador == null || operador.Teclado == null)
                Assert.Ignore("la escena no tiene el operador digital del variador");

            yield return Cablear();
            _sim.SaltarConfiguracion = true;
            _sim.SaltarCableado = true;
            _sim.AplicarPuntoDePartida();
            yield return Cablear();
            yield return null;

            // El alumno toca la tecla que no entiende, con el motor parado (es cuando el
            // V1000 la admite).
            operador.Pulsar(VfdKey.LoRe);
            yield return null;
            Assert.IsTrue(_sim.Vfd.ModoLocal, "LO/RE debería haber pasado el variador a LOCAL");

            // Y ahora intenta operar el tablero como le dice la guía.
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);
            yield return new WaitForSeconds(2f);

            // El tablero PARECE vivo.
            Assert.AreEqual(1, _sim.Plc.NivelActivo, "el PLC sube de nivel igual");
            Assert.IsTrue(_sim.Plc.SalidaV(1), "y el indicador V1 se enciende");
            Assert.IsTrue(_sim.Plc.SalidaS1, "y manda la orden de marcha por S1");
            Assert.IsTrue(_sim.Puerta.S1, "que además llega al borne, porque el cableado está bien");

            // Pero el motor no gira.
            Assert.AreEqual(0f, _sim.Vfd.FrecuenciaSalidaHz, 0.01f,
                "en LOCAL el variador ignora los terminales: el motor no gira. " + _sim.Resumen());

            // DEFECTO ENCONTRADO: nada en el simulador explica por qué. El validador de
            // configuración, que es quien da las explicaciones didácticas de «por qué no
            // arranca», no mira ModoLocal.
            var avisos = _sim.Vfd.ValidarConfiguracion();
            Assert.Greater(avisos.Count, 0,
                "DEFECTO: con el variador en LOCAL el tablero no arranca y ValidarConfiguracion() "
                + "no da ningún aviso, así que la guía sigue diciendo que todo está bien y el "
                + "paso «variador configurado» de la práctica 2 se da por cumplido");
        }

        [UnityTest]
        public IEnumerator EnLocalConRun_ElStopDelTableroNoPara_PeroElHongoSi()
        {
            var operador = _sim.Operador;
            if (operador == null || operador.Teclado == null)
                Assert.Ignore("la escena no tiene el operador digital del variador");

            yield return Cablear();
            operador.Pulsar(VfdKey.LoRe);
            operador.Pulsar(VfdKey.Run);
            yield return new WaitForSeconds(2f);

            Assert.IsTrue(_sim.Vfd.ModoLocal, "debería estar en LOCAL");
            Assert.Greater(_sim.Vfd.FrecuenciaSalidaHz, 0f,
                "en LOCAL la tecla RUN arranca el motor aunque el PLC esté en reposo. " + _sim.Resumen());
            Assert.AreEqual(0, _sim.Plc.NivelActivo, "y el PLC ni se ha enterado");

            // El STOP del tablero (que va al PLC) no tiene nada que decir aquí.
            _sim.PulsarStop();
            yield return new WaitForSeconds(1f);
            Assert.Greater(_sim.Vfd.FrecuenciaSalidaHz, 0f,
                "documentado a propósito: el STOP del tablero actúa sobre el PLC, y en LOCAL el "
                + "variador no escucha al PLC. Es fiel al V1000 real, pero el alumno ve un botón "
                + "de parada que no para. " + _sim.Resumen());

            _sim.PulsarEmergencia();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, _sim.Vfd.FrecuenciaSalidaHz, 0.01f,
                "el hongo tiene que cortar también en LOCAL: corta por hardware. " + _sim.Resumen());
        }

        /// <summary>
        /// Provocar una avería de la práctica 5 con el motor girando a toda velocidad.
        ///
        /// En el recorrido normal la avería se aplica al empezar la práctica, con el módulo
        /// parado. Aquí se mete en caliente, que es lo que haría el docente para enseñarla en
        /// vivo. No puede reventar ni dejar el módulo irreparable.
        /// </summary>
        [UnityTest]
        public IEnumerator ProvocarAveriasConElMotorGirando_NoRompeNadaYSeRepara()
        {
            yield return Cablear();
            var fuente = _sim.Cables;

            foreach (var falla in VdfCatalogoFallas.Crear())
            {
                // Módulo sano y girando a la cuarta velocidad.
                _sim.SaltarConfiguracion = true;
                _sim.SaltarCableado = true;
                _sim.AplicarPuntoDePartida();
                yield return Cablear();
                _sim.PulsarStop();
                yield return null;
                _sim.PulsarStart();
                for (int n = 1; n <= PlcFanLogic.NivelMaximo; n++)
                {
                    _sim.PulsarSubir();
                    yield return EsperarNivel(n);
                }
                yield return new WaitForSeconds(1f);

                // Y se rompe en caliente.
                Assert.IsTrue(falla.Aplicar(_sim.Vfd, fuente != null ? fuente.Grafo : null),
                    "la avería '" + falla.Id + "' debería poder aplicarse");
                _sim.RevisarCableado();
                yield return new WaitForSeconds(0.5f);

                Assert.IsTrue(falla.EstaPresente(_sim.Vfd, fuente.Grafo),
                    "la avería '" + falla.Id + "' debería notarse tras aplicarla en caliente");
                Assert.IsNotNull(_sim.Resumen(), "y el resumen de estado debería seguir funcionando");

                // Y el módulo tiene que poder repararse sin reiniciar la aplicación.
                _sim.SaltarConfiguracion = true;
                _sim.SaltarCableado = true;
                _sim.AplicarPuntoDePartida();
                yield return Cablear();
                yield return null;
                Assert.IsTrue(falla.Reparada(_sim.Vfd, fuente.Grafo),
                    "la avería '" + falla.Id + "' debería quedar reparada por el punto de partida");
            }
        }

        // =====================================================================================
        // 6. Valores límite en la lógica pura
        // =====================================================================================

        /// <summary>
        /// Avanzar el PLC con dt absurdos: enorme, negativo e infinito.
        ///
        /// Un dt enorme es real: el primer fotograma tras cargar la escena en una Quest, o
        /// tras un parón del sistema operativo, puede valer décimas de segundo. Lo que no
        /// puede pasar es que un dt raro se salte el retardo de 3 s (la seguridad del
        /// ejercicio depende de él) ni que lo deje colgado para siempre.
        /// </summary>
        [UnityTest]
        public IEnumerator ElPlcConDtAbsurdos_NoSeSaltaNiCuelgaElRetardo()
        {
            var plc = new PlcFanLogic();

            // Un dt negativo no debe hacer avanzar ni retroceder nada.
            plc.PulsarStart();
            plc.PulsarSubir();
            plc.Tick(-5f);
            plc.Tick(-1000f);
            Assert.IsTrue(plc.CambioEnCurso, "un dt negativo no puede resolver el cambio");
            Assert.AreEqual(0, plc.NivelActivo, "ni arrancar el motor");

            // Un dt enorme resuelve el retardo, pero no se pasa de nivel.
            plc.Tick(1000f);
            Assert.AreEqual(1, plc.NivelActivo, "un dt de mil segundos debería aplicar el nivel pedido");
            Assert.IsFalse(plc.CambioEnCurso, "y no dejar nada pendiente");

            // Infinito: igual de absurdo, mismo resultado.
            plc.PulsarSubir();
            plc.Tick(float.PositiveInfinity);
            Assert.AreEqual(2, plc.NivelActivo, "un dt infinito debería resolver el cambio, no romperlo");

            // Y después de todo eso el PLC sigue obedeciendo.
            plc.PulsarStop();
            Assert.AreEqual(0, plc.NivelActivo, "STOP debería seguir funcionando");
            yield break;
        }

        // =====================================================================================
        // 7. Huecos de validación de la API
        //
        // Lo que sigue NO se alcanza desde la interfaz: ningún clic, tecla ni mando puede
        // producir un NaN ni un identificador nulo. Están aquí porque son agujeros reales de
        // validación con síntomas feos, y porque cerrarlos cuesta una línea cada uno. Van
        // aparte para poder triarlos con menos prioridad que los defectos de arriba.
        // =====================================================================================

        /// <summary>
        /// DEFECTO ENCONTRADO (hueco de validación) — el variador acepta <c>float.NaN</c> en
        /// cualquier parámetro sin rechistar, y eso envenena la frecuencia de salida.
        ///
        /// <c>VfdParameterDef.EsValido</c> comprueba <c>valor &lt; Min || valor &gt; Max</c>, y
        /// las dos comparaciones son falsas con NaN, así que el valor pasa el filtro y se
        /// guarda. Escribiendo NaN en E1-04, la frecuencia de salida se vuelve NaN; en la
        /// escena eso llega al <c>Quaternion.Euler</c> del eje del motor, que es un error de
        /// Unity por fotograma.
        ///
        /// Se prueba sobre un modelo suelto a propósito, para no llenar la consola de errores
        /// durante la tanda de tests.
        /// </summary>
        [UnityTest]
        public IEnumerator ElVariadorAceptaNaN_YSeLeEnvenenaLaFrecuencia()
        {
            var vfd = new VfdModel();
            vfd.AplicarConfiguracionDeReferencia();

            // Los valores imposibles "normales" sí se rechazan, como debe ser.
            Assert.IsFalse(vfd.EscribirParametro("E1-04", -1f), "un negativo debería rechazarse");
            Assert.IsFalse(vfd.EscribirParametro("E1-04", float.MaxValue), "y un valor gigante también");
            Assert.IsFalse(vfd.EscribirParametro("E1-04", float.PositiveInfinity), "y el infinito");

            // DEFECTO ENCONTRADO: NaN se cuela.
            Assert.IsFalse(vfd.EscribirParametro("E1-04", float.NaN),
                "DEFECTO: EsValido() compara con Min y Max, y las dos comparaciones son falsas "
                + "con NaN, así que NaN se guarda como si fuera un valor legítimo");

            // Y esto es lo que pasa cuando se cuela: el motor acaba girando NaN grados.
            vfd.SetTerminal(1, true);
            for (int i = 0; i < 10; i++) vfd.Tick(0.1f);
            Assert.IsFalse(float.IsNaN(vfd.FrecuenciaSalidaHz),
                "DEFECTO: con E1-04 = NaN la frecuencia de salida se vuelve NaN. En la escena "
                + "eso va al Quaternion del eje del motor y Unity escupe un error por frame");
            yield break;
        }

        /// <summary>
        /// DEFECTO ENCONTRADO (hueco de validación) — un identificador nulo tira
        /// <c>ArgumentNullException</c> en vez de devolver false.
        ///
        /// <c>VdfWiringGraph.Conectar</c> sí trata el nulo con cuidado (devuelve false), pero
        /// las capas de encima lo meten directo en un <c>Dictionary</c>, que con una clave
        /// nula lanza. Lo mismo en la tabla de parámetros. Es inalcanzable desde la interfaz,
        /// pero es una excepción esperando a que alguien conecte un dato de fuera.
        /// </summary>
        [UnityTest]
        public IEnumerator IdentificadoresNulos_DeberianDevolverFalseEnVezDeReventar()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");
            _cableado.IndexarBornes();

            // Lo que sí está bien resuelto: el grafo y la cadena vacía.
            Assert.IsFalse(_sim.Cables.Grafo.Conectar(null, "S1"), "el grafo ya trata el nulo");
            Assert.IsFalse(_cableado.Conectar("", "S1"), "y la cadena vacía no encuentra borne");
            Assert.IsNull(_cableado.BuscarBorne(""), "buscar la cadena vacía debería dar null");
            Assert.AreEqual(0, _cableado.BorrarCablesDe(""), "y borrar por cadena vacía, cero");

            // DEFECTO ENCONTRADO: con null, en cambio, revienta.
            Assert.IsNull(_cableado.BuscarBorne(null),
                "DEFECTO: BuscarBorne(null) mete null como clave en un Dictionary y lanza "
                + "ArgumentNullException en vez de devolver null");

            string error;
            Assert.IsFalse(_sim.Vfd.EscribirParametro(null, 1f, out error),
                "DEFECTO: EscribirParametro(null, ...) lanza ArgumentNullException desde "
                + "VfdParameterTable.TryGet en vez de devolver false");
            yield break;
        }

        // =====================================================================================
        // Herramientas
        // =====================================================================================

        /// <summary>Cuenta los objetos vivos de la escena, para detectar fugas.</summary>
        private static int ContarObjetos()
        {
            return Object.FindObjectsByType<Transform>().Length;
        }

        private static int ContarCables()
        {
            return Object.FindObjectsByType<VdfCablePhysics>().Length;
        }

        /// <summary>Ids de todos los bornes de la escena, en orden estable.</summary>
        private static List<string> IdsDeBornes()
        {
            var ids = new List<string>();
            var bornes = Object.FindObjectsByType<VdfTerminal>();
            for (int i = 0; i < bornes.Length; i++)
                if (!string.IsNullOrEmpty(bornes[i].Id) && !ids.Contains(bornes[i].Id))
                    ids.Add(bornes[i].Id);

            ids.Sort();   // FindObjectsByType no garantiza orden: así el ataque es repetible
            return ids;
        }

        private static Transform BuscarEje()
        {
            var motor = GameObject.Find(VdfPanelBuilder.NombreMotor);
            return motor != null ? motor.transform.Find("Eje") : null;
        }

        private IEnumerator Cablear()
        {
            if (_cableado == null) yield break;

            _cableado.IndexarBornes();
            _cableado.BorrarTodo();

            foreach (var red in VdfWiringSpec.Practica())
                for (int i = 0; i + 1 < red.Bornes.Length; i++)
                    _cableado.Conectar(red.Bornes[i], red.Bornes[i + 1]);

            _cableado.AvisarCableadoCambiado();
            _sim.RevisarCableado();
            yield return null;
        }

        private IEnumerator EsperarNivel(int nivel)
        {
            float limite = PlcFanLogic.RetardoCambioNivelSegundos + 2f;
            float t = 0f;
            while (_sim.Plc.NivelActivo != nivel && t < limite)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
