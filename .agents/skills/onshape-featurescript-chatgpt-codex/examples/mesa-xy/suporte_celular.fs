FeatureScript 3083;
import(path : "onshape/std/geometry.fs", version : "3083.0");
import(path : "onshape/std/properties.fs", version : "3083.0");

// =====================================================================
// Suporte do celular - montagem no lugar (um Part Studio, tudo posicionado)
//
// Um celular fica deitado, tela para cima, sobre duas barras lisas de aco
// (60 mm entre centros) com a camera olhando para a mesa XY. Duas "selas"
// andam nos dois tubos laterais de um quadro de metalon e seguram as barras;
// duas "garras" deslizam nas duas barras e apertam o celular pelos dois lados
// compridos. Parafusos M6 rosqueiam direto no furo piloto impresso (sem porca).
// Corpos gerados: quadro de metalon, 2 selas, 2 barras, 2 garras, celular.
// O usuario exporta uma sela e uma garra em STL (a outra de cada par e a mesma
// peca girada 180 graus em Z).
//
// Mundo (Z para cima, mm): quadro centrado em X = 0, Y = 0, base em Z = 0. As
// barras correm em X; o metalon sob a sela corre em Y.
//   Sela:  Z local 0 = topo do tubo; X local 0 = centro do tubo.
//   Garra: Z local 0 = eixo das barras; X local 0 = face interna da parede;
//          celular no lado +X local.
// sela() e garra() recebem a origem (ox, oy, oz) e o sinal sx (+1 ou -1) que
// multiplica toda coordenada X local; ambas sao simetricas em Y, entao sx = -1
// equivale a mesma peca girada 180 graus em Z.
// =====================================================================

const MARGEM = 1;            // quanto a ferramenta de corte estoura a parede que atravessa
const PAREDE = 2.5;          // parede de plastico ao redor dos furos das barras
const EVA = 3;               // espessura da espuma entre garra e celular (ASSUMIDO)
const DIA_BARRA_REAL = 7.8;  // diametro real da barra lisa (INFORMADO)

// -------------------- primitivas --------------------

// Bloco entre dois cantos em qualquer ordem (mm).
function makeBlock(context is Context, id is Id, a is Vector, b is Vector) returns Query
{
    fCuboid(context, id, {
            "corner1" : vector(min(a[0], b[0]), min(a[1], b[1]), min(a[2], b[2])) * millimeter,
            "corner2" : vector(max(a[0], b[0]), max(a[1], b[1]), max(a[2], b[2])) * millimeter
    });
    return qCreatedBy(id, EntityType.BODY);
}

function cylinder(context is Context, id is Id, a is Vector, b is Vector, radius is number) returns Query
{
    fCylinder(context, id, {
            "bottomCenter" : a * millimeter,
            "topCenter" : b * millimeter,
            "radius" : radius * millimeter
    });
    return qCreatedBy(id, EntityType.BODY);
}

// Cilindro centrado em "center", ao longo de "X", "Y" ou "Z".
function cylinderOnAxis(context is Context, id is Id, center is Vector, axis is string,
        diameter is number, length is number) returns Query
{
    var n;
    if (axis == "X")
    {
        n = vector(1, 0, 0);
    }
    else if (axis == "Y")
    {
        n = vector(0, 1, 0);
    }
    else
    {
        n = vector(0, 0, 1);
    }
    return cylinder(context, id, center - n * (length / 2), center + n * (length / 2), diameter / 2);
}

// Une "additions" ao alvo e depois subtrai "cuts". Continue usando "target"
// como Query do corpo resultante (o Id do boolean nao e um corpo novo).
function combine(context is Context, id is Id, target is Query, additions is array, cuts is array)
{
    if (size(additions) > 0)
    {
        opBoolean(context, id + "join", {
                "tools" : qUnion(concatenateArrays([[target], additions])),
                "operationType" : BooleanOperationType.UNION
        });
    }
    if (size(cuts) > 0)
    {
        opBoolean(context, id + "cut", {
                "targets" : target,
                "tools" : qUnion(cuts),
                "operationType" : BooleanOperationType.SUBTRACTION
        });
    }
}

// Nome + cor de uma vez: todo corpo passa por aqui.
function nomear(context is Context, body is Query, label is string, cor is Color)
{
    setProperty(context, {
            "entities" : body,
            "propertyType" : PropertyType.NAME,
            "value" : label
    });
    setProperty(context, {
            "entities" : body,
            "propertyType" : PropertyType.APPEARANCE,
            "value" : cor
    });
}

// Referencial de uma peca: origem (ox, oy, oz) e sinal sx do eixo X local.
function refPeca(ox is number, oy is number, oz is number, sx is number) returns map
{
    return { "ox" : ox, "oy" : oy, "oz" : oz, "sx" : sx };
}

// Ponto local (x, y, z) da peca no mundo (mm).
function loc(f is map, x is number, y is number, z is number) returns Vector
{
    return vector(f.ox + f.sx * x, f.oy + y, f.oz + z);
}

// -------------------- cotas: FONTE UNICA --------------------

// Converte a definition (com unidade) em numeros puros em mm, num lugar so.
function cotas(d is map) returns map
{
    var tuboLargura = d.tuboLargura / millimeter;
    var tuboAltura = d.tuboAltura / millimeter;
    var folgaTubo = d.folgaTubo / millimeter;
    var diaBarra = d.diaBarra / millimeter;
    var diaBarraSela = d.diaBarraSela / millimeter;
    var entreBarras = d.entreBarras / millimeter;
    var quadroZ = d.quadroZ / millimeter;
    var perna = 3;
    var wi = tuboLargura / 2 + folgaTubo;
    var zBoreSela = 2 + diaBarraSela / 2;   // 2 mm de placa entre o tubo e o furo da barra
    return {
        "tuboLargura" : tuboLargura,
        "tuboAltura" : tuboAltura,
        "entreBarras" : entreBarras,
        "diaBarra" : diaBarra,
        "diaBarraSela" : diaBarraSela,
        "furo" : d.furoRosca / millimeter,
        "abertura" : d.abertura / millimeter,
        "janela" : d.janela / millimeter,
        "quadroX" : d.quadroX / millimeter,
        "quadroY" : d.quadroY / millimeter,
        "quadroZ" : quadroZ,
        "comprimentoBarra" : d.comprimentoBarra / millimeter,
        "posX" : d.posX / millimeter,
        "posY" : d.posY / millimeter,
        "celularLargura" : d.celularLargura / millimeter,
        "celularComprimento" : d.celularComprimento / millimeter,
        "celularEspessura" : d.celularEspessura / millimeter,
        "comprimento" : entreBarras + 14,
        "wi" : wi,
        "wo" : wi + perna,
        "alturaPerna" : 8,
        "topo" : 2 + diaBarraSela + PAREDE,
        "bolsoY" : entreBarras / 2 - diaBarraSela / 2 - PAREDE,   // meia-largura do alivio entre as barras
        "zBoreSela" : zBoreSela,
        "zBarra" : quadroZ + zBoreSela,
        "zb" : diaBarra / 2 + PAREDE
    };
}

// -------------------- pecas --------------------

// Quadro de metalon: 4 tubos no topo, 4 na base e 4 postes, tudo em UM corpo.
// Cada junta e um cubo inteiro de tubo em comum (sem tangencia).
function quadro(context is Context, id is Id, c is map)
{
    var t = c.tuboLargura;
    var tA = c.tuboAltura;
    var hx = c.quadroX / 2;
    var hy = c.quadroY / 2;
    var pecas = [];
    var i = 0;
    for (var s in [-1, 1])
    {
        var cx = s * (hx - t / 2);
        var cy = s * (hy - t / 2);
        for (var zIni in [0, c.quadroZ - tA])
        {
            pecas = append(pecas, makeBlock(context, id + ("quadroTuboX" ~ i),
                        vector(-hx, cy - t / 2, zIni), vector(hx, cy + t / 2, zIni + tA)));
            pecas = append(pecas, makeBlock(context, id + ("quadroTuboY" ~ i),
                        vector(cx - t / 2, -hy, zIni), vector(cx + t / 2, hy, zIni + tA)));
            i += 1;
        }
    }
    var j = 0;
    for (var sx in [-1, 1])
    {
        for (var sy in [-1, 1])
        {
            pecas = append(pecas, makeBlock(context, id + ("quadroPoste" ~ j),
                        vector(sx * (hx - t / 2) - t / 2, sy * (hy - t / 2) - t / 2, 0),
                        vector(sx * (hx - t / 2) + t / 2, sy * (hy - t / 2) + t / 2, c.quadroZ)));
            j += 1;
        }
    }
    opBoolean(context, id + "quadroUniao", {
            "tools" : qUnion(pecas),
            "operationType" : BooleanOperationType.UNION
    });
    nomear(context, pecas[0], "Quadro metalon", color(0.25, 0.25, 0.27));
}

// Sela do metalon: U leve (abre para baixo). As barras ficam presas por atrito
// em dois furos justos; um alivio no teto entre as barras tira plastico; um furo
// piloto M6 horizontal na perna +X local, dentro de um boss externo, trava a sela
// no tubo.
function sela(context is Context, id is Id, c is map, ox is number, oy is number, oz is number,
        sx is number, label is string)
{
    var f = refPeca(ox, oy, oz, sx);
    var meioY = c.entreBarras / 2;
    var meioComp = c.comprimento / 2;
    var zTrava = -c.alturaPerna / 2;

    var corpo = makeBlock(context, id + "selaCorpo",
            loc(f, -c.wo, -meioComp, -c.alturaPerna), loc(f, c.wo, meioComp, c.topo));

    var canal = makeBlock(context, id + "selaCanal",
            loc(f, -c.wi, -meioComp - MARGEM, -c.alturaPerna - MARGEM), loc(f, c.wi, meioComp + MARGEM, 0));

    var larguraBarra = 2 * c.wo + 2 * MARGEM;
    var barraA = cylinderOnAxis(context, id + "selaBarraA", loc(f, 0, -meioY, c.zBoreSela), "X",
            c.diaBarraSela, larguraBarra);
    var barraB = cylinderOnAxis(context, id + "selaBarraB", loc(f, 0, meioY, c.zBoreSela), "X",
            c.diaBarraSela, larguraBarra);
    var cuts = [canal, barraA, barraB];

    // alivio no teto entre as barras: deixa uma placa de 3 mm sobre o tubo
    if (2 * c.bolsoY > 5)
    {
        cuts = append(cuts, makeBlock(context, id + "selaBolso",
                loc(f, -c.wo - MARGEM, -c.bolsoY, 3), loc(f, c.wo + MARGEM, c.bolsoY, c.topo + MARGEM)));
    }

    // boss externo (diametro 13) mergulha 0,5 mm na perna +X local
    var boss = cylinder(context, id + "selaBoss", loc(f, c.wo - 0.5, 0, zTrava), loc(f, c.wo + 6, 0, zTrava), 6.5);
    cuts = append(cuts, cylinder(context, id + "selaTravaTubo", loc(f, c.wi - MARGEM, 0, zTrava),
            loc(f, c.wo + 6 + MARGEM, 0, zTrava), c.furo / 2));

    combine(context, id + "selaBool", corpo, [boss], cuts);
    nomear(context, corpo, label, color(0.05, 0.60, 0.60));
}

// Garra do celular: C leve. Corpo que desliza nas barras (o ressalto X [0, 10]
// apoia o celular) + parede fina com janela + labio que prende o celular + dois
// bosses baixos para os parafusos M6 de trava nas barras.
function garra(context is Context, id is Id, c is map, ox is number, oy is number, oz is number,
        sx is number, label is string)
{
    var f = refPeca(ox, oy, oz, sx);
    var meioY = c.entreBarras / 2;
    var meioComp = c.comprimento / 2;
    var zb = c.zb;

    var corpo = makeBlock(context, id + "garraCorpo",
            loc(f, -14, -meioComp, -zb), loc(f, 10, meioComp, zb));

    // parede mergulha 0,5 mm (em Z) no corpo
    var parede = makeBlock(context, id + "garraParede",
            loc(f, -3, -meioComp, zb - 0.5), loc(f, 0, meioComp, zb + c.abertura + 0.5));
    // labio mergulha 0,5 mm (em Z) na parede
    var labio = makeBlock(context, id + "garraLabio",
            loc(f, -3, -meioComp, zb + c.abertura), loc(f, 9, meioComp, zb + c.abertura + 3));
    var adicoes = [parede, labio];

    var cuts = [];
    var larguraBarra = 24 + 2 * MARGEM;
    var i = 0;
    for (var y in [-meioY, meioY])
    {
        // boss mergulha 0,5 mm (em Z) no corpo; fica 0,5 mm afastado da parede em X
        adicoes = append(adicoes, makeBlock(context, id + ("garraBoss" ~ i),
                loc(f, -14, y - 7, zb - 0.5), loc(f, -3.5, y + 7, zb + 6)));
        cuts = append(cuts, cylinderOnAxis(context, id + ("garraBarra" ~ i), loc(f, -2, y, 0), "X",
                c.diaBarra, larguraBarra));
        cuts = append(cuts, cylinder(context, id + ("garraPiloto" ~ i), loc(f, -8.5, y, 0),
                loc(f, -8.5, y, zb + 6 + MARGEM), c.furo / 2));
        i += 1;
    }
    if (c.janela > 0)
    {
        // janela vazada na parede para nao apertar os botoes laterais
        cuts = append(cuts, makeBlock(context, id + "garraJanela",
                loc(f, -3 - MARGEM, -c.janela / 2, zb), loc(f, MARGEM, c.janela / 2, zb + c.abertura)));
    }

    combine(context, id + "garraBool", corpo, adicoes, cuts);
    nomear(context, corpo, label, color(1.0, 0.45, 0.30));
}

// Barra lisa de aco ao longo de X, centrada em X = 0, no eixo Y = y.
function barra(context is Context, id is Id, c is map, y is number, label is string)
{
    var corpo = cylinderOnAxis(context, id, vector(0, y, c.zBarra), "X", DIA_BARRA_REAL, c.comprimentoBarra);
    nomear(context, corpo, label, color(0.78, 0.78, 0.80));
}

// Celular (referencia): caixa apoiada no ressalto das garras + bump da camera
// embaixo (tela para cima, camera voltada para a mesa).
function celular(context is Context, id is Id, c is map)
{
    var zFundo = c.zBarra + c.zb;
    var corpo = makeBlock(context, id + "celularCorpo",
            vector(c.posX - c.celularLargura / 2, c.posY - c.celularComprimento / 2, zFundo),
            vector(c.posX + c.celularLargura / 2, c.posY + c.celularComprimento / 2, zFundo + c.celularEspessura));

    // bump de 12 mm: sai 1,5 mm abaixo da face de baixo e mergulha 0,5 mm no celular
    var xCam = c.posX - c.celularLargura / 2 + 15;
    var yCam = c.posY + c.celularComprimento / 2 - 15;
    var camera = cylinder(context, id + "celularCamera", vector(xCam, yCam, zFundo - 1.5),
            vector(xCam, yCam, zFundo + 0.5), 6);

    combine(context, id + "celularBool", corpo, [camera], []);
    nomear(context, corpo, "Celular (referencia)", color(0.08, 0.08, 0.09));
}

// -------------------- feature --------------------

annotation { "Feature Type Name" : "Suporte do celular - montagem" }
export const suporteCelular = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Largura do metalon (X)" }
        isLength(definition.tuboLargura, { (millimeter) : [10, 20, 50] } as LengthBoundSpec);

        annotation { "Name" : "Altura do metalon (Z)" }
        isLength(definition.tuboAltura, { (millimeter) : [10, 20, 50] } as LengthBoundSpec);

        annotation { "Name" : "Folga por lado no tubo" }
        isLength(definition.folgaTubo, { (millimeter) : [0.1, 0.4, 1.5] } as LengthBoundSpec);

        annotation { "Name" : "Diametro do furo da barra na garra (desliza)" }
        isLength(definition.diaBarra, { (millimeter) : [7, 8.2, 10] } as LengthBoundSpec);

        annotation { "Name" : "Diametro do furo da barra na sela (justo)" }
        isLength(definition.diaBarraSela, { (millimeter) : [7.6, 7.9, 8.4] } as LengthBoundSpec);

        annotation { "Name" : "Distancia entre barras" }
        isLength(definition.entreBarras, { (millimeter) : [30, 60, 120] } as LengthBoundSpec);

        annotation { "Name" : "Furo piloto M6" }
        isLength(definition.furoRosca, { (millimeter) : [4.5, 5, 6.5] } as LengthBoundSpec);

        annotation { "Name" : "Abertura da garra (espessura do celular)" }
        isLength(definition.abertura, { (millimeter) : [8, 14, 25] } as LengthBoundSpec);

        annotation { "Name" : "Janela nos botoes (0 = sem janela)" }
        isLength(definition.janela, { (millimeter) : [0, 40, 70] } as LengthBoundSpec);

        annotation { "Name" : "Quadro - comprimento em X (ao longo das barras)" }
        isLength(definition.quadroX, { (millimeter) : [300, 535, 800] } as LengthBoundSpec);

        annotation { "Name" : "Quadro - largura em Y" }
        isLength(definition.quadroY, { (millimeter) : [200, 385, 600] } as LengthBoundSpec);

        annotation { "Name" : "Quadro - altura em Z" }
        isLength(definition.quadroZ, { (millimeter) : [100, 210, 400] } as LengthBoundSpec);

        annotation { "Name" : "Comprimento das barras lisas" }
        isLength(definition.comprimentoBarra, { (millimeter) : [400, 700, 1000] } as LengthBoundSpec);

        annotation { "Name" : "Posicao do celular em X (ao longo das barras)" }
        isLength(definition.posX, { (millimeter) : [-150, 0, 150] } as LengthBoundSpec);

        annotation { "Name" : "Posicao das selas e do celular em Y (ao longo dos tubos)" }
        isLength(definition.posY, { (millimeter) : [-120, 0, 120] } as LengthBoundSpec);

        annotation { "Name" : "Celular - largura (X)" }
        isLength(definition.celularLargura, { (millimeter) : [60, 75, 95] } as LengthBoundSpec);

        annotation { "Name" : "Celular - comprimento (Y)" }
        isLength(definition.celularComprimento, { (millimeter) : [120, 160, 200] } as LengthBoundSpec);

        annotation { "Name" : "Celular - espessura (Z)" }
        isLength(definition.celularEspessura, { (millimeter) : [6, 9, 14] } as LengthBoundSpec);
    }
    {
        var c = cotas(definition);

        if (c.furo + 2 > c.alturaPerna)
        {
            throw regenError("Furo piloto nao cabe na perna da sela: aumente Altura do metalon ou diminua Furo piloto M6.");
        }
        if (c.alturaPerna >= c.tuboAltura)
        {
            throw regenError("Perna da sela mais alta que o tubo: aumente Altura do metalon.");
        }
        if (c.entreBarras <= c.diaBarra + 6 || c.entreBarras <= c.diaBarraSela + 6)
        {
            throw regenError("Barras muito proximas: aumente Distancia entre barras ou diminua os diametros dos furos das barras (garra ou sela).");
        }
        if (c.janela >= c.comprimento - 32)
        {
            throw regenError("Janela grande demais para a garra: diminua Janela nos botoes.");
        }
        if (c.furo / 2 + 1.5 > 5)
        {
            throw regenError("Furo piloto nao cabe no boss da garra: diminua Furo piloto M6.");
        }
        if (c.diaBarra < DIA_BARRA_REAL)
        {
            throw regenError("A barra real nao passa no furo: aumente Diametro do furo da barra na garra.");
        }
        if (c.celularEspessura > c.abertura)
        {
            throw regenError("Celular mais grosso que a abertura da garra: diminua Celular - espessura ou aumente Abertura da garra.");
        }
        if (abs(c.posY) + c.comprimento / 2 > c.quadroY / 2 - c.tuboLargura)
        {
            throw regenError("Sela bate nos tubos em X do quadro: reduza Posicao em Y ou aumente Quadro - largura em Y.");
        }
        if (abs(c.posX) + c.celularLargura / 2 + EVA + 14 > c.quadroX / 2 - c.tuboLargura / 2 - c.wo)
        {
            throw regenError("Garra bate na sela: reduza Posicao do celular em X ou Celular - largura, ou aumente Quadro - comprimento em X.");
        }
        if (c.comprimentoBarra < c.quadroX - c.tuboLargura + 2 * c.wo)
        {
            throw regenError("A barra nao atravessa as duas selas: aumente Comprimento das barras lisas ou diminua Quadro - comprimento em X.");
        }

        var xSela = c.quadroX / 2 - c.tuboLargura / 2;
        var xGarra = c.celularLargura / 2 + EVA;

        quadro(context, id, c);
        sela(context, id + "selaA", c, xSela, c.posY, c.quadroZ, 1, "Sela do metalon A");
        sela(context, id + "selaB", c, -xSela, c.posY, c.quadroZ, -1, "Sela do metalon B");
        barra(context, id + "barraA", c, c.posY - c.entreBarras / 2, "Barra lisa A");
        barra(context, id + "barraB", c, c.posY + c.entreBarras / 2, "Barra lisa B");
        garra(context, id + "garraA", c, c.posX - xGarra, c.posY, c.zBarra, 1, "Garra do celular A");
        garra(context, id + "garraB", c, c.posX + xGarra, c.posY, c.zBarra, -1, "Garra do celular B");
        celular(context, id + "celular", c);
    });
