﻿grammar HolyJava;

program: line* EOF;

line: statement;
lineWithoutFunctionDef: statementWithoutFunctionDef;

statement: (topLevelStatements | returnStatement | bottomLevelStatements | classKeywords | funcCall);
statementWithoutFunctionDef: varAssignment | varDeclaration | returnStatement 
| bottomLevelStatements | classKeywords | funcCall;

topLevelStatements: (varAssignment | varDeclaration | funcDefinition);
bottomLevelStatements: (ifLogic | loopKeywords);

varKeywords: (varReassignment | varAssignment | varDeclaration);
funcKeywords: (funcDefinition | funcCall);
loopKeywords: (forLoop | whileLoop);
classKeywords: (classDef | classVar | classVarReassignment | classAccess);

virtualKeyword: 'virtual';
abstractKeyword: 'abstract';
overrideKeyword: 'override';

classDef: abstractKeyword? 'class' IDENTIFIER ('extends' IDENTIFIER)? '{' topLevelStatements* '}';
classVar: IDENTIFIER IDENTIFIER ';';
classVarReassignment: IDENTIFIER '=' IDENTIFIER ';';
classAccess: IDENTIFIER '.' (funcCall | varReassignment | varAssignment | IDENTIFIER) ';'?;

returnStatement: 'return' expression ';';

varReassignment: IDENTIFIER '=' expression ';';
varAssignment: varParameter '=' expression ';';
varDeclaration: varParameter ';';
varParameter: varType IDENTIFIER;
varType: PRIMATIVE_INT | PRIMATIVE_STRING | PRIMATIVE_FLOAT | PRIMATIVE_BOOL;

ifLogic: 'if' '(' expression ')' block elseIfLogic* elseLogic?;
elseIfLogic: 'else if' '(' expression ')' block;
elseLogic: 'else' block;

forLoop: 'for' '(' varAssignment expression ';' expression')' block;
whileLoop: 'while' '(' expression ')' block;

funcDefinition: overrideKeyword? (virtualKeyword? abstractKeyword? | abstractKeyword? virtualKeyword?)
	'func' IDENTIFIER '(' (varDeclaration (',' varDeclaration)*)? ')' ('->' varType)? (block | ';');
funcCall: IDENTIFIER '(' (expression (',' expression)*)? ')' ';'?;

expression
: funcCall								#funcCallExpression
| classAccess							#varAccessExpression
| IDENTIFIER							#identifierExpression
| expression multOp expression          #multiplicativeExpression
| expression comparisonOps expression   #comparisonExpression
| expression addOp expression	        #additiveExpression
| constant								#constantExpression
;

multOp: '*' | '/';
addOp: '+' | '-';
comparisonOps: '<' | '>' | '<=' | '>=' | '==' | '!=';
constant: INT | STRING | FLOAT | BOOL;

PRIMATIVE_INT: 'int';
PRIMATIVE_FLOAT: 'float';
PRIMATIVE_STRING: 'string';
PRIMATIVE_BOOL: 'bool';

INT: (NEG_MOD NEG_MOD)? [0-9]+;
FLOAT: (NEG_MOD NEG_MOD)? [0-9]+ '.' [0-9]+;
STRING: ('"' ~'"'* '"') | ('\'' ~'\''* '\'');
BOOL: 'true' | 'false';
NULL: 'null';

NEG_MOD: '-';

block: '{' lineWithoutFunctionDef* '}';
IDENTIFIER: [a-zA-Z@_] [a-zA-Z0-9_]*;
WS: [ \t\r\n]+ -> skip;