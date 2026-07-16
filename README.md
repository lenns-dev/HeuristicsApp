# HeuristicsApp

Sistema de Optimización de Procesos de Batching y Routing en Entornos de Almacenamiento


​Descripción del Proyecto

​Este proyecto aborda el problema de la optimización logística en la preparación de pedidos (Order Picking), específicamente en la secuenciación y agrupación de órdenes (Order Batching Problem - OBP) y la posterior determinación de rutas óptimas de recogida (Picker Routing Problem - PRP).

​El sistema permite configurar diferentes distribuciones espaciales de almacenes (layouts), evaluar la eficacia y eficiencia de diversas heurísticas y metaheurísticas en C#, y analizar los resultados mediante un motor de visualización web vectorial interactivo desarrollado sobre la arquitectura ASP.NET Core Razor Pages.


​Estructura de la Memoria Técnica / Índice del Repositorio

​El repositorio está organizado siguiendo la estructura formal para la validación académica del proyecto:

​/Core: Contiene los algoritmos heurísticos y sus modelos relacionados.
/WebApi: Se encarga de mostrar y oftecer via HTTPS/HTTP las funcionalidades extraídas de Core a la parte web
/Web: Utiliza las funcionalidades ofrecidas por la WebApi para ejecutar los algoritmos escogidos por el usuario sobre los warehouses escogidos y mostrar una representación gráfica del resultado.


Marco Tecnológico y Arquitectura
​La solución se ha diseñado bajo un enfoque de separación de conceptos (Separation of Concerns), garantizando la modularidad para facilitar las pruebas unitarias:

​Backend: .NET Core (C#) enfocado en la computación algorítmica y cálculo, usando programación orientada a objetos.

​Frontend: ASP.NET Core Razor Pages, implementando manipulación dinámica del Modelo de Objetos del Documento (DOM) y renderizado vectorial interactivo mediante SVG (Scalable Vector Graphics) y HTML5 Canvas para la representación de rutas y mapas de calor

​Estándar de Estilos: CSS3 estructurado para la disposición de paneles y métricas.


​Componente Algorítmico y Heurísticas

​El núcleo del proyecto evalúa el impacto combinado de las fases de agrupación y enrutamiento:

​1. Algoritmos de Batching
//TODO
​
​2. Algoritmos de Routing (Enrutamiento de Operarios)
​//TODO


​Indicadores de Rendimiento y Eficiencia (KPIs)

​Para evaluar la viabilidad de las soluciones propuestas, el módulo analítico procesa y contrasta las siguientes métricas:
//TODO