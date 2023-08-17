﻿grammar HolyJava;

program: line* EOF;

line: statement;
lineWithoutFunctionDef: statementWithoutFunctionDef;

statement: (topLevelStatements | returnStatement | bottomLevelStatements | includeKeyword
| classKeywords | funcCall | includeCall | ppMM);
statementWithoutFunctionDef: varKeywords | returnStatement 
| bottomLevelStatements | classKeywords | funcCall | includeKeyword | ppMM;

topLevelStatements: (varKeywords | arrayKeywords | funcDefinition);
bottomLevelStatements: (ifLogic | loopKeywords | arrayKeywords);

skipMod: '#';
overrideMod: '!';
includeKeyword: (skipMod | overrideMod)? 'include' STRING ('as' IDENTIFIER)? ';';
includeCall: IDENTIFIER '::' (funcCall | ppMM | arrayCall | classAccess | arraySet | varReassignment | IDENTIFIER) ';'?;

ppMM: IDENTIFIER topLevelAddOp;

varKeywords: (varReassignment | varAssignment | varDeclaration);
funcKeywords: (funcDefinition | funcCall);
loopKeywords: (forLoop | whileLoop);
classKeywords: (classDef | classVar | classVarReassignment | classAccess);
arrayKeywords: (arrayDefinition | arraySet);

virtualKeyword: 'virtual';
abstractKeyword: 'abstract';
overrideKeyword: 'override';

constructor: IDENTIFIER '(' (varDeclaration (',' varDeclaration)*)? ')' block;
staticKeyword: 'static';
classDef: staticKeyword? abstractKeyword? 'class' IDENTIFIER ('extends' IDENTIFIER)? '{' constructor topLevelStatements* '}';
classVar: (IDENTIFIER | includeCall) IDENTIFIER ('=' (IDENTIFIER | includeCall) '(' (expression (',' expression)*)? ')')? ';';
classVarReassignment: IDENTIFIER '=' IDENTIFIER ';';
classAccess: IDENTIFIER '.' (funcCall | arrayCall | arraySet | varReassignment | varAssignment | IDENTIFIER) ';'?;

returnStatement: 'return' expression ';';

varReassignment: IDENTIFIER '=' expression ';';
varAssignment: varParameter '=' expression ';';
varDeclaration: varParameter ';';
varParameter: varType IDENTIFIER;
varType: PRIMATIVE_INT | PRIMATIVE_STRING | PRIMATIVE_FLOAT | PRIMATIVE_BOOL | PRIMATIVE_OBJECT;

arrayDefinition: varParameter '=' varType '[' INT ']' ';';
arrayCall: IDENTIFIER '[' INT ']';
arraySet: IDENTIFIER '[' INT ']' '=' expression ';';

ifLogic: 'if' '(' expression ')' block elseIfLogic* elseLogic?;
elseIfLogic: 'else if' '(' expression ')' block;
elseLogic: 'else' block;

forLoop: 'for' '(' varAssignment expression ';' expression')' block;
whileLoop: 'while' '(' expression ')' block;

funcDefinition: overrideKeyword? (virtualKeyword? abstractKeyword? | abstractKeyword? virtualKeyword?)
	'func' IDENTIFIER '(' (varDeclaration (',' varDeclaration)*)? ')' ('->' varType)? (block | ';');
funcCall: IDENTIFIER '(' (expression (',' expression)*)? ')' ';'?;

expression
: funcCall											#funcCallExpression
| includeCall										#includeCallExpression
| arrayCall											#arrayCallExpression
| classAccess										#varAccessExpression
| IDENTIFIER										#identifierExpression
| expression multOp expression						#multiplicativeExpression
| expression comparisonOps expression				#comparisonExpression
| expression (addOp | topLevelAddOp) expression		#additiveExpression
| constant											#constantExpression
;

multOp: '*' | '/';
addOp: '+' | '-';
topLevelAddOp: '++' | '--';
comparisonOps: '<' | '>' | '<=' | '>=' | '==' | '!=';
constant: INT | STRING | FLOAT | BOOL;

PRIMATIVE_INT: 'int';
PRIMATIVE_FLOAT: 'float';
PRIMATIVE_STRING: 'string';
PRIMATIVE_BOOL: 'bool';
PRIMATIVE_OBJECT: 'object';

INT: (NEG_MOD NEG_MOD)? [0-9]+;
FLOAT: (NEG_MOD NEG_MOD)? [0-9]+ '.' [0-9]+;
STRING: ('"' ~'"'* '"') | ('\'' ~'\''* '\'');
BOOL: 'true' | 'false';
OBJECT: (INT | FLOAT | STRING | BOOL);
NULL: 'null';

NEG_MOD: '-';

block: '{' lineWithoutFunctionDef* '}';
IDENTIFIER: [a-zA-Z@_] [a-zA-Z0-9_]*;
WS: [ \t\r\n]+ -> skip;