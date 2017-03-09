using System;
using System.Management.Automation.Language;
using SourcemapToolkit.SourcemapParser;
using System.Collections.Generic;

public class SourceMapper : ICustomAstVisitor {
	private SourceMap Map { get; set; }

	public ScriptBlockAst MapFunctions(ScriptBlockAst CombinedScript, SourceMap SourceMap) {
		this.Map = SourceMap;

		return (ScriptBlockAst)this.VisitScriptBlock(CombinedScript);
	}

	public SourcePosition GetSourcePosition(int line, int column ) {
		SourcePosition sourcePosition = new SourcePosition {
			ZeroBasedLineNumber = line,
			ZeroBasedColumnNumber = column
		};
		return sourcePosition;
	}

	public IScriptExtent MapExtent(IScriptExtent extent) {
		if(extent == null) {
			return null;
		}

		//Console.WriteLine("StartText: '{0}'", extent.StartScriptPosition.Line);

		int columnCorrection = 0;
		if(extent.StartLineNumber != extent.EndLineNumber) {
			columnCorrection = -1;
		}

		ScriptPosition startSourcePosition = null;
		if(extent.StartScriptPosition != null) {
			//Console.Write("mapping start {0}:{1} => ", extent.StartLineNumber, extent.StartColumnNumber);
			SourcePosition startGeneratedPosition = GetSourcePosition(extent.StartLineNumber - 1, 0);
			MappingEntry startMappingEntry = Map.GetMappingEntryForGeneratedSourcePosition(startGeneratedPosition);
			if(startMappingEntry == null) {
				startSourcePosition = new ScriptPosition(
					extent.File,
					extent.StartLineNumber,
					extent.StartColumnNumber + columnCorrection,
					extent.StartScriptPosition.Line
				);
			} else {
				//Console.WriteLine("{0}:{1}", startMappingEntry.OriginalSourcePosition.ZeroBasedLineNumber + 1, extent.StartColumnNumber);
				startSourcePosition = new ScriptPosition(
					startMappingEntry.OriginalFileName,
					startMappingEntry.OriginalSourcePosition.ZeroBasedLineNumber + 1,
					extent.StartColumnNumber + columnCorrection,
					extent.StartScriptPosition.Line
				);
			}
		}

		//Console.WriteLine("EndText: '{0}'", extent.EndScriptPosition.Line);

		ScriptPosition endSourcePosition = null;
		if(extent.EndScriptPosition != null) {
			//Console.Write("mapping end {0}:{1} => ", extent.EndLineNumber, extent.EndColumnNumber);
			SourcePosition endGeneratedPosition = GetSourcePosition(extent.EndLineNumber - 1, 0);
			MappingEntry endMappingEntry = Map.GetMappingEntryForGeneratedSourcePosition(endGeneratedPosition);
			if(endMappingEntry == null) {
				endSourcePosition = new ScriptPosition(
					extent.File,
					extent.EndLineNumber,
					extent.EndColumnNumber + columnCorrection,
					extent.EndScriptPosition.Line
				);
			} else {
				//Console.WriteLine("{0}:{1}", endMappingEntry.OriginalSourcePosition.ZeroBasedLineNumber + 1, extent.EndColumnNumber);
				int endColumnNumber = extent.EndColumnNumber;
				/*
				if(extent.EndColumnNumber == 1 && extent.EndScriptPosition.Line == String.Empty) {
					endColumnNumber = 0;
				}
				*/
				endSourcePosition= new ScriptPosition(
					endMappingEntry.OriginalFileName,
					endMappingEntry.OriginalSourcePosition.ZeroBasedLineNumber + 1,
					endColumnNumber + columnCorrection,
					extent.EndScriptPosition.Line
				);
			}
		}

		IScriptExtent newExtent = new ScriptExtent(startSourcePosition, endSourcePosition);
		//Console.WriteLine("Returning mapped extent from {0}:{1} to {2}:{3} - {4}", startSourcePosition.LineNumber, startSourcePosition.ColumnNumber, endSourcePosition.LineNumber, endSourcePosition.ColumnNumber, newExtent.Text);
		return newExtent;
	}

	public System.Object VisitArrayExpression(System.Management.Automation.Language.ArrayExpressionAst arrayExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(arrayExpressionAst.Extent);

		StatementBlockAst mappedStatementBlock = (StatementBlockAst)VisitStatementBlock(arrayExpressionAst.SubExpression);
		return new System.Management.Automation.Language.ArrayExpressionAst(mappedExtent, mappedStatementBlock);
	}

	public  System.Object VisitArrayLiteral(System.Management.Automation.Language.ArrayLiteralAst arrayLiteralAst) {
		IScriptExtent mappedExtent = MapExtent(arrayLiteralAst.Extent);

		List<ExpressionAst> mappedExpressions = new List<ExpressionAst>();
		foreach(ExpressionAst expression in arrayLiteralAst.Elements) {
			mappedExpressions.Add(_VisitExpression(expression));
		}

		return new ArrayLiteralAst(mappedExtent, mappedExpressions);
	}

	public ExpressionAst _VisitExpression(ExpressionAst expressionAst) {
		//Console.WriteLine("Visiting expression type {0}", expressionAst.GetType().FullName);
		var @switch = new Dictionary<Type, Func<ExpressionAst,ExpressionAst>> {
			{ typeof(ArrayExpressionAst), (e) => { return (ExpressionAst)VisitArrayExpression(e as ArrayExpressionAst);} },
			{ typeof(ArrayLiteralAst), (e) => { return (ExpressionAst)VisitArrayLiteral(e as ArrayLiteralAst);} },
			{ typeof(AttributedExpressionAst), (e) => { return (ExpressionAst)VisitAttributedExpression(e as AttributedExpressionAst);} },
				//AttributedExpressionAst
				{ typeof(ConvertExpressionAst), (e) => { return (ExpressionAst)VisitConvertExpression(e as ConvertExpressionAst);} },
			{ typeof(BinaryExpressionAst), (e) => { return (ExpressionAst)VisitBinaryExpression(e as BinaryExpressionAst);} },
			{ typeof(ConstantExpressionAst), (e) => { return (ExpressionAst)VisitConstantExpression(e as ConstantExpressionAst);} },
				//ConstantExpressionAst
				{ typeof(StringConstantExpressionAst), (e) => { return (ExpressionAst)VisitStringConstantExpression(e as StringConstantExpressionAst);} },
			{ typeof(ErrorExpressionAst), (e) => { return (ExpressionAst)VisitErrorExpression(e as ErrorExpressionAst);} },
			{ typeof(ExpandableStringExpressionAst), (e) => { return (ExpressionAst)VisitExpandableStringExpression(e as ExpandableStringExpressionAst);} },
			{ typeof(HashtableAst), (e) => { return (ExpressionAst)VisitHashtable(e as HashtableAst);} },
			{ typeof(IndexExpressionAst), (e) => { return (ExpressionAst)VisitIndexExpression(e as IndexExpressionAst);} },
			{ typeof(MemberExpressionAst), (e) => { return (ExpressionAst)VisitMemberExpression(e as MemberExpressionAst);} },
				//MemberExpressionAst
				{ typeof(InvokeMemberExpressionAst), (e) => { return (ExpressionAst)VisitInvokeMemberExpression(e as InvokeMemberExpressionAst);} },
			{ typeof(ParenExpressionAst), (e) => { return (ExpressionAst)VisitParenExpression(e as ParenExpressionAst);} },
			{ typeof(ScriptBlockExpressionAst), (e) => { return (ExpressionAst)VisitScriptBlockExpression(e as ScriptBlockExpressionAst);} },
			{ typeof(SubExpressionAst), (e) => { return (ExpressionAst)VisitSubExpression(e as SubExpressionAst);} },
			{ typeof(TypeExpressionAst), (e) => { return (ExpressionAst)VisitTypeExpression(e as TypeExpressionAst);} },
			{ typeof(UnaryExpressionAst), (e) => { return (ExpressionAst)VisitUnaryExpression(e as UnaryExpressionAst);} },
			{ typeof(UsingExpressionAst), (e) => { return (ExpressionAst)VisitUsingExpression(e as UsingExpressionAst);} },
			{ typeof(VariableExpressionAst), (e) => { return (ExpressionAst)VisitVariableExpression(e as VariableExpressionAst);} }
		};
		if(expressionAst != null && @switch.ContainsKey(expressionAst.GetType())) {
			return @switch[expressionAst.GetType()](expressionAst);
		} else {
			//Console.WriteLine("_VisitExpression may not know how to visit type {0}", expressionAst.GetType().FullName);
			return null;
		}
	}


	public  System.Object VisitAssignmentStatement(System.Management.Automation.Language.AssignmentStatementAst assignmentStatementAst) {
		IScriptExtent mappedExtent = MapExtent(assignmentStatementAst.Extent);

		IScriptExtent mappedErrorExtent = MapExtent(assignmentStatementAst.ErrorPosition);

		ExpressionAst mappedLeft = _VisitExpression(assignmentStatementAst.Left);
		StatementAst mappedRight = _VisitStatement(assignmentStatementAst.Right);

		return new AssignmentStatementAst(mappedExtent, mappedLeft, assignmentStatementAst.Operator, mappedRight, mappedErrorExtent);
	}

	public StatementAst _VisitStatement(StatementAst statementAst) {
		//Console.WriteLine("Visiting statement type {0}", statementAst.GetType().FullName);
		var @switch = new Dictionary<Type, Func<StatementAst,StatementAst>> {
			{ typeof(BlockStatementAst), (s) => { return (StatementAst)VisitBlockStatement(s as BlockStatementAst);} },
			{ typeof(BreakStatementAst), (s) => { return (StatementAst)VisitBreakStatement(s as BreakStatementAst);} },
			{ typeof(CommandAst), (s) => { return (StatementAst)VisitCommand(s as CommandAst);} },
			{ typeof(CommandExpressionAst), (s) => { return (StatementAst)VisitCommandExpression(s as CommandExpressionAst);} },
			{ typeof(ContinueStatementAst), (s) => { return (StatementAst)VisitContinueStatement(s as ContinueStatementAst);} },
			{ typeof(DataStatementAst), (s) => { return (StatementAst)VisitDataStatement(s as DataStatementAst);} },
			{ typeof(ExitStatementAst), (s) => { return (StatementAst)VisitExitStatement(s as ExitStatementAst);} },
			{ typeof(FunctionDefinitionAst), (s) => { return (StatementAst)VisitFunctionDefinition(s as FunctionDefinitionAst);} },
			{ typeof(IfStatementAst), (s) => { return (StatementAst)VisitIfStatement(s as IfStatementAst);} },
				//LabeledStatementAst
					//LoopStatementAst
					{ typeof(DoUntilStatementAst), (s) => { return (StatementAst)VisitDoUntilStatement(s as DoUntilStatementAst);} },
					{ typeof(DoWhileStatementAst), (s) => { return (StatementAst)VisitDoWhileStatement(s as DoWhileStatementAst);} },
					{ typeof(ForEachStatementAst), (s) => { return (StatementAst)VisitForEachStatement(s as ForEachStatementAst);} },
					{ typeof(ForStatementAst), (s) => { return (StatementAst)VisitForStatement(s as ForStatementAst);} },
					{ typeof(WhileStatementAst), (s) => { return (StatementAst)VisitWhileStatement(s as WhileStatementAst);} },
				{ typeof(SwitchStatementAst), (s) => { return (StatementAst)VisitSwitchStatement(s as SwitchStatementAst);} },
				//PipelineBaseAst
					{ typeof(AssignmentStatementAst), (s) => { return (StatementAst)VisitAssignmentStatement(s as AssignmentStatementAst);} },
					{ typeof(ErrorStatementAst), (s) => { return (StatementAst)VisitErrorStatement(s as ErrorStatementAst);} },
					{ typeof(PipelineAst), (s) => { return (StatementAst)VisitPipeline(s as PipelineAst);} },
			{ typeof(ReturnStatementAst), (s) => { return (StatementAst)VisitReturnStatement(s as ReturnStatementAst);} },
			{ typeof(ThrowStatementAst), (s) => { return (StatementAst)VisitThrowStatement(s as ThrowStatementAst);} },
			{ typeof(TrapStatementAst), (s) => { return (StatementAst)VisitTrap(s as TrapStatementAst);} },
			{ typeof(TryStatementAst), (s) => { return (StatementAst)VisitTryStatement(s as TryStatementAst);} }
		};

		return @switch[statementAst.GetType()](statementAst);
	}

	public  System.Object VisitAttribute(System.Management.Automation.Language.AttributeAst attributeAst) {
		IScriptExtent mappedExtent = MapExtent(attributeAst.Extent);

		LinkedList<ExpressionAst> mappedPositionalArguments = new LinkedList<ExpressionAst>();
		foreach(ExpressionAst e in attributeAst.PositionalArguments) {
			mappedPositionalArguments.AddLast(_VisitExpression(e));
		}

		LinkedList<NamedAttributeArgumentAst> mappedNamedArguments = new LinkedList<NamedAttributeArgumentAst>();
		foreach(NamedAttributeArgumentAst na in attributeAst.NamedArguments) {
			mappedNamedArguments.AddLast((NamedAttributeArgumentAst)VisitNamedAttributeArgument(na));
		}

		return new AttributeAst(mappedExtent, attributeAst.TypeName, mappedPositionalArguments, mappedNamedArguments);
	}


	public  System.Object VisitAttributedExpression(System.Management.Automation.Language.AttributedExpressionAst attributedExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(attributedExpressionAst.Extent);

		AttributeBaseAst mappedAttribute = _VisitAttributeBase(attributedExpressionAst.Attribute);
		ExpressionAst mappedChild = _VisitExpression(attributedExpressionAst.Child);

		return new AttributedExpressionAst(mappedExtent, mappedAttribute, mappedChild);
	}

	public AttributeBaseAst _VisitAttributeBase(AttributeBaseAst attributeBaseAst) {
		var @switch = new Dictionary<Type, Func<AttributeBaseAst,AttributeBaseAst>> {
			{ typeof(AttributeAst), (ab) => { return (AttributeBaseAst)VisitAttribute(ab as AttributeAst);} },
			{ typeof(TypeConstraintAst), (ab) => { return (AttributeBaseAst)VisitTypeConstraint(ab as TypeConstraintAst);} }
		};

		return @switch[attributeBaseAst.GetType()](attributeBaseAst);
	}


	public  System.Object VisitBinaryExpression(System.Management.Automation.Language.BinaryExpressionAst binaryExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(binaryExpressionAst.Extent);
		IScriptExtent mappedErrorExtent = MapExtent(binaryExpressionAst.ErrorPosition);

		ExpressionAst mappedLeft = _VisitExpression(binaryExpressionAst.Left);
		ExpressionAst mappedRight = _VisitExpression(binaryExpressionAst.Right);

		return new BinaryExpressionAst(mappedExtent, mappedLeft, binaryExpressionAst.Operator, mappedRight, mappedErrorExtent);
	}


	public  System.Object VisitBlockStatement(System.Management.Automation.Language.BlockStatementAst blockStatementAst) {
		IScriptExtent mappedExtent = MapExtent(blockStatementAst.Extent);

		StatementBlockAst mappedBody = (StatementBlockAst)VisitStatementBlock(blockStatementAst.Body);

		return new BlockStatementAst(mappedExtent, blockStatementAst.Kind, mappedBody);
	}


	public  System.Object VisitBreakStatement(System.Management.Automation.Language.BreakStatementAst breakStatementAst) {
		IScriptExtent mappedExtent = MapExtent(breakStatementAst.Extent);

		ExpressionAst mappedLabel = breakStatementAst.Label;

		return new BreakStatementAst(mappedExtent, mappedLabel);
	}

	public  System.Object VisitCatchClause(System.Management.Automation.Language.CatchClauseAst catchClauseAst) {
		IScriptExtent mappedExtent = MapExtent(catchClauseAst.Extent);

		LinkedList<TypeConstraintAst> mappedCatchTypes = new LinkedList<TypeConstraintAst>();
		foreach(TypeConstraintAst tc in catchClauseAst.CatchTypes) {
			mappedCatchTypes.AddLast((TypeConstraintAst)VisitTypeConstraint(tc));
		}
 		StatementBlockAst mappedBody = (StatementBlockAst)VisitStatementBlock(catchClauseAst.Body);


		return new CatchClauseAst(mappedExtent, mappedCatchTypes, mappedBody);
	}

	public  System.Object VisitCommand(System.Management.Automation.Language.CommandAst commandAst) {
		IScriptExtent mappedExtent = MapExtent(commandAst.Extent);

		LinkedList<CommandElementAst> mappedCommandElements = new LinkedList<CommandElementAst>();
		foreach(CommandElementAst ce in commandAst.CommandElements) {
			mappedCommandElements.AddLast(_VisitCommandElement(ce));
		}
		LinkedList<RedirectionAst> mappedRedirections = new LinkedList<RedirectionAst>();
		foreach(RedirectionAst r in commandAst.Redirections) {
			mappedRedirections.AddLast(_VisitRedirection(r));
		}

		return new CommandAst(mappedExtent, mappedCommandElements, commandAst.InvocationOperator, mappedRedirections);
	}

	public CommandElementAst _VisitCommandElement(CommandElementAst commandElementAst) {
		//Console.WriteLine("Visiting commandelement type {0}", commandElementAst.GetType().FullName);
		Type commandType = commandElementAst.GetType();
		if(commandType.IsSubclassOf(typeof(ExpressionAst))) {
			commandType = typeof(ExpressionAst);
		}
		var @switch = new Dictionary<Type, Func<CommandElementAst,CommandElementAst>> {
			{ typeof(CommandParameterAst), (ce) => { return (CommandElementAst)VisitCommandParameter(ce as CommandParameterAst);} },
			{ typeof(ExpressionAst), (ce) => { return (CommandElementAst)_VisitExpression(ce as ExpressionAst);} }
		};

		return @switch[commandType](commandElementAst);
	}

	public RedirectionAst _VisitRedirection(RedirectionAst redirectionAst) {
		var @switch = new Dictionary<Type, Func<RedirectionAst,RedirectionAst>> {
			{ typeof(FileRedirectionAst), (r) => { return (RedirectionAst)VisitFileRedirection(r as FileRedirectionAst);} },
			{ typeof(MergingRedirectionAst), (r) => { return (RedirectionAst)VisitMergingRedirection(r as MergingRedirectionAst);} }
		};

		return @switch[redirectionAst.GetType()](redirectionAst);
	}

	public  System.Object VisitCommandExpression(System.Management.Automation.Language.CommandExpressionAst commandExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(commandExpressionAst.Extent);

		ExpressionAst mappedExpression = _VisitExpression(commandExpressionAst.Expression);
		LinkedList<RedirectionAst> mappedRedirections = new LinkedList<RedirectionAst>();
		foreach(RedirectionAst r in commandExpressionAst.Redirections) {
			mappedRedirections.AddLast(_VisitRedirection(r));
		}

		return new CommandExpressionAst(mappedExtent, mappedExpression, mappedRedirections);
	}

	public  System.Object VisitCommandParameter(System.Management.Automation.Language.CommandParameterAst commandParameterAst) {
		IScriptExtent mappedExtent = MapExtent(commandParameterAst.Extent);

		ExpressionAst mappedArgument = commandParameterAst.Argument != null ? _VisitExpression(commandParameterAst.Argument) : null;
		IScriptExtent mappedErrorPosition = MapExtent(commandParameterAst.ErrorPosition);


		return new CommandParameterAst(mappedExtent, commandParameterAst.ParameterName, mappedArgument, mappedErrorPosition);
	}

	public  System.Object VisitConstantExpression(System.Management.Automation.Language.ConstantExpressionAst constantExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(constantExpressionAst.Extent);

		return new ConstantExpressionAst(mappedExtent, constantExpressionAst.Value);
	}

	public  System.Object VisitContinueStatement(System.Management.Automation.Language.ContinueStatementAst continueStatementAst) {
		IScriptExtent mappedExtent = MapExtent(continueStatementAst.Extent);

		ExpressionAst mappedLabel = _VisitExpression(continueStatementAst.Label);

		return new ContinueStatementAst(mappedExtent, mappedLabel);
	}

	public  System.Object VisitConvertExpression(System.Management.Automation.Language.ConvertExpressionAst convertExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(convertExpressionAst.Extent);

		TypeConstraintAst mappedTypeConstraint = (TypeConstraintAst)VisitTypeConstraint(convertExpressionAst.Attribute as TypeConstraintAst);
		ExpressionAst mappedChild = _VisitExpression(convertExpressionAst.Child);


		return new ConvertExpressionAst(mappedExtent, mappedTypeConstraint, mappedChild);
	}

	public  System.Object VisitDataStatement(System.Management.Automation.Language.DataStatementAst dataStatementAst) {
		IScriptExtent mappedExtent = MapExtent(dataStatementAst.Extent);

		LinkedList<ExpressionAst> mappedCommandsAllowed = new LinkedList<ExpressionAst>();
		foreach(ExpressionAst e in dataStatementAst.CommandsAllowed) {
			mappedCommandsAllowed.AddLast(_VisitExpression(e));
		}
		StatementBlockAst mappedBody = (StatementBlockAst)VisitStatementBlock(dataStatementAst.Body);


		return new DataStatementAst(mappedExtent, dataStatementAst.Variable, mappedCommandsAllowed, mappedBody);
	}

	public  System.Object VisitDoUntilStatement(System.Management.Automation.Language.DoUntilStatementAst doUntilStatementAst) {
		IScriptExtent mappedExtent = MapExtent(doUntilStatementAst.Extent);

		PipelineBaseAst mappedCondition = _VisitPipelineBase(doUntilStatementAst.Condition);
		StatementBlockAst mappedBody = (StatementBlockAst)VisitStatementBlock(doUntilStatementAst.Body);


		return new DoUntilStatementAst(mappedExtent, doUntilStatementAst.Label, mappedCondition, mappedBody);
	}

	public PipelineBaseAst _VisitPipelineBase(PipelineBaseAst pipelineBaseAst) {
		var @switch = new Dictionary<Type, Func<PipelineBaseAst,PipelineBaseAst>> {
			{ typeof(AssignmentStatementAst), (pb) => { return (PipelineBaseAst)VisitAssignmentStatement(pb as AssignmentStatementAst);} },
			{ typeof(ErrorStatementAst), (pb) => { return (PipelineBaseAst)VisitErrorStatement(pb as ErrorStatementAst);} },
			{ typeof(PipelineAst), (pb) => { return (PipelineBaseAst)VisitPipeline(pb as PipelineAst);} }
		};

		return @switch[pipelineBaseAst.GetType()](pipelineBaseAst);
	}

	public  System.Object VisitDoWhileStatement(System.Management.Automation.Language.DoWhileStatementAst doWhileStatementAst) {
		IScriptExtent mappedExtent = MapExtent(doWhileStatementAst.Extent);

		PipelineBaseAst mappedCondition = _VisitPipelineBase(doWhileStatementAst.Condition);
		StatementBlockAst mappedBody = (StatementBlockAst)VisitStatementBlock(doWhileStatementAst.Body);

		return new DoWhileStatementAst(mappedExtent, doWhileStatementAst.Label, mappedCondition, mappedBody);
	}

	public  System.Object VisitErrorExpression(System.Management.Automation.Language.ErrorExpressionAst errorExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(errorExpressionAst.Extent);

		return errorExpressionAst;
	}

	public  System.Object VisitErrorStatement(System.Management.Automation.Language.ErrorStatementAst errorStatementAst) {
		IScriptExtent mappedExtent = MapExtent(errorStatementAst.Extent);

		return errorStatementAst;
	}

	public  System.Object VisitExitStatement(System.Management.Automation.Language.ExitStatementAst exitStatementAst) {
		IScriptExtent mappedExtent = MapExtent(exitStatementAst.Extent);

		PipelineBaseAst mappedPipeline = _VisitPipelineBase(exitStatementAst.Pipeline);

		return new ExitStatementAst(mappedExtent, mappedPipeline);
	}

	public  System.Object VisitExpandableStringExpression(System.Management.Automation.Language.ExpandableStringExpressionAst expandableStringExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(expandableStringExpressionAst.Extent);

		return new ExpandableStringExpressionAst(mappedExtent, expandableStringExpressionAst.Value, expandableStringExpressionAst.StringConstantType);
	}

	public  System.Object VisitFileRedirection(System.Management.Automation.Language.FileRedirectionAst fileRedirectionAst) {
		IScriptExtent mappedExtent = MapExtent(fileRedirectionAst.Extent);

		ExpressionAst mappedFile = _VisitExpression(fileRedirectionAst.Location);

		return new FileRedirectionAst(mappedExtent, fileRedirectionAst.FromStream, mappedFile, fileRedirectionAst.Append);
	}

	public  System.Object VisitForEachStatement(System.Management.Automation.Language.ForEachStatementAst forEachStatementAst) {
		IScriptExtent mappedExtent = MapExtent(forEachStatementAst.Extent);

		VariableExpressionAst mappedVariable = (VariableExpressionAst)VisitVariableExpression(forEachStatementAst.Variable);
		PipelineBaseAst mappedExpression = _VisitPipelineBase(forEachStatementAst.Condition);
		StatementBlockAst mappedBody = (StatementBlockAst)VisitStatementBlock(forEachStatementAst.Body);

		return new ForEachStatementAst(mappedExtent, forEachStatementAst.Label, forEachStatementAst.Flags, mappedVariable, mappedExpression, mappedBody);
	}

	public  System.Object VisitForStatement(System.Management.Automation.Language.ForStatementAst forStatementAst) {
		IScriptExtent mappedExtent = MapExtent(forStatementAst.Extent);

		PipelineBaseAst mappedInitializer = _VisitPipelineBase(forStatementAst.Initializer);
		PipelineBaseAst mappedCondition = _VisitPipelineBase(forStatementAst.Condition);
		PipelineBaseAst mappedIterator = _VisitPipelineBase(forStatementAst.Iterator);
		StatementBlockAst mappedBody = (StatementBlockAst)VisitStatementBlock(forStatementAst.Body);


		return new ForStatementAst(mappedExtent, forStatementAst.Label, mappedInitializer, mappedCondition, mappedIterator, mappedBody);
	}

	public  System.Object VisitFunctionDefinition(System.Management.Automation.Language.FunctionDefinitionAst functionDefinitionAst) {
		IScriptExtent mappedExtent = MapExtent(functionDefinitionAst.Extent);

		LinkedList<ParameterAst> mappedParameters = new LinkedList<ParameterAst>();
		if(functionDefinitionAst.Parameters != null) {
			foreach(ParameterAst p in functionDefinitionAst.Parameters) {
				mappedParameters.AddLast((ParameterAst)VisitParameter(p));
			}
		}
		ScriptBlockAst mappedBody = (ScriptBlockAst)VisitScriptBlock(functionDefinitionAst.Body);

		return new FunctionDefinitionAst(mappedExtent, functionDefinitionAst.IsFilter, functionDefinitionAst.IsWorkflow, functionDefinitionAst.Name, mappedParameters, mappedBody);
	}

	public  System.Object VisitHashtable(System.Management.Automation.Language.HashtableAst hashtableAst) {
		IScriptExtent mappedExtent = MapExtent(hashtableAst.Extent);

		LinkedList<Tuple<ExpressionAst, StatementAst>> mappedKeyValuePairs = new LinkedList<Tuple<ExpressionAst, StatementAst>>();
		foreach(Tuple<ExpressionAst, StatementAst> kvpair in hashtableAst.KeyValuePairs) {
			mappedKeyValuePairs.AddLast(
				new Tuple<ExpressionAst, StatementAst>(
					_VisitExpression(kvpair.Item1),
					_VisitStatement(kvpair.Item2)
				)
			);
		}

		return new HashtableAst(mappedExtent, mappedKeyValuePairs);
	}

	public  System.Object VisitIfStatement(System.Management.Automation.Language.IfStatementAst ifStmtAst) {
		IScriptExtent mappedExtent = MapExtent(ifStmtAst.Extent);

		LinkedList<Tuple<PipelineBaseAst, StatementBlockAst>> mappedClauses = new LinkedList<Tuple<PipelineBaseAst, StatementBlockAst>>();
		foreach(Tuple<PipelineBaseAst, StatementBlockAst> c in ifStmtAst.Clauses) {
			mappedClauses.AddLast(
				new Tuple<PipelineBaseAst, StatementBlockAst>(
					_VisitPipelineBase(c.Item1),
					(StatementBlockAst)VisitStatementBlock(c.Item2)
				)
			);
		}
		StatementBlockAst mappedElseClause = ifStmtAst.ElseClause != null ? (StatementBlockAst)VisitStatementBlock(ifStmtAst.ElseClause) : null;

		return new IfStatementAst(mappedExtent, mappedClauses, mappedElseClause);
	}

	public  System.Object VisitIndexExpression(System.Management.Automation.Language.IndexExpressionAst indexExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(indexExpressionAst.Extent);

		ExpressionAst mappedTarget = _VisitExpression(indexExpressionAst.Target);
		ExpressionAst mappedIndex = _VisitExpression(indexExpressionAst.Index);

		return new IndexExpressionAst(mappedExtent, mappedTarget, mappedIndex);
	}

	public  System.Object VisitInvokeMemberExpression(System.Management.Automation.Language.InvokeMemberExpressionAst invokeMemberExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(invokeMemberExpressionAst.Extent);

		ExpressionAst mappedExpression = _VisitExpression(invokeMemberExpressionAst.Expression);
		CommandElementAst mappedMethod = _VisitCommandElement(invokeMemberExpressionAst.Member);
		LinkedList<ExpressionAst> mappedArguments = new LinkedList<ExpressionAst>();
		if(invokeMemberExpressionAst.Arguments != null) {
			foreach(ExpressionAst e in invokeMemberExpressionAst.Arguments) {
				mappedArguments.AddLast(_VisitExpression(e));
			}
		}

		return new InvokeMemberExpressionAst(mappedExtent, mappedExpression, mappedMethod, mappedArguments, invokeMemberExpressionAst.Static);
	}

	public  System.Object VisitMemberExpression(System.Management.Automation.Language.MemberExpressionAst memberExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(memberExpressionAst.Extent);

		ExpressionAst mappedExpression = _VisitExpression(memberExpressionAst.Expression);
		CommandElementAst mappedMember = _VisitCommandElement(memberExpressionAst.Member);

		return new MemberExpressionAst(mappedExtent, mappedExpression, mappedMember, memberExpressionAst.Static);
	}

	public  System.Object VisitMergingRedirection(System.Management.Automation.Language.MergingRedirectionAst mergingRedirectionAst) {
		IScriptExtent mappedExtent = MapExtent(mergingRedirectionAst.Extent);

		return new MergingRedirectionAst(mappedExtent, mergingRedirectionAst.FromStream, mergingRedirectionAst.ToStream);
	}

	public  System.Object VisitNamedAttributeArgument(System.Management.Automation.Language.NamedAttributeArgumentAst namedAttributeArgumentAst) {
		IScriptExtent mappedExtent = MapExtent(namedAttributeArgumentAst.Extent);

		ExpressionAst mappedArgument = _VisitExpression(namedAttributeArgumentAst.Argument);

		return new NamedAttributeArgumentAst(mappedExtent, namedAttributeArgumentAst.ArgumentName, mappedArgument, namedAttributeArgumentAst.ExpressionOmitted);
	}

	public  System.Object VisitNamedBlock(System.Management.Automation.Language.NamedBlockAst namedBlockAst) {
		IScriptExtent mappedExtent = MapExtent(namedBlockAst.Extent);

		LinkedList<StatementAst> mappedStatements = new LinkedList<StatementAst>();
		foreach(StatementAst s in namedBlockAst.Statements) {
			mappedStatements.AddLast(_VisitStatement(s));
		}
		LinkedList<TrapStatementAst> mappedTraps = new LinkedList<TrapStatementAst>();
		if(namedBlockAst.Traps != null) {
			foreach(TrapStatementAst ts in namedBlockAst.Traps) {
				mappedTraps.AddLast((TrapStatementAst)VisitTrap(ts));
			}
		}
		// this doesn't really map the statement block
		StatementBlockAst mappedStatementBlock = new StatementBlockAst(mappedExtent, mappedStatements, mappedTraps);

		return new NamedBlockAst(mappedExtent, namedBlockAst.BlockKind, mappedStatementBlock, namedBlockAst.Unnamed);
	}

	public  System.Object VisitParamBlock(System.Management.Automation.Language.ParamBlockAst paramBlockAst) {
		IScriptExtent mappedExtent = MapExtent(paramBlockAst.Extent);

		LinkedList<AttributeAst> mappedAttributes = new LinkedList<AttributeAst>();
		foreach(AttributeAst a in paramBlockAst.Attributes) {
			mappedAttributes.AddLast((AttributeAst)VisitAttribute(a));
		}
		LinkedList<ParameterAst> mappedParameters = new LinkedList<ParameterAst>();
		foreach(ParameterAst p in paramBlockAst.Parameters) {
			mappedParameters.AddLast((ParameterAst)VisitParameter(p));
		}

		return new ParamBlockAst(mappedExtent, mappedAttributes, mappedParameters);
	}

	public  System.Object VisitParameter(System.Management.Automation.Language.ParameterAst parameterAst) {
		IScriptExtent mappedExtent = MapExtent(parameterAst.Extent);

		VariableExpressionAst mappedName = (VariableExpressionAst)VisitVariableExpression(parameterAst.Name);
		LinkedList<AttributeBaseAst> mappedAttributes = new LinkedList<AttributeBaseAst>();
		foreach(AttributeBaseAst ab in parameterAst.Attributes) {
			mappedAttributes.AddLast(_VisitAttributeBase(ab));
		}
		ExpressionAst mappedDefaultValue = parameterAst.DefaultValue != null ? _VisitExpression(parameterAst.DefaultValue) : null;

		return new ParameterAst(mappedExtent, mappedName, mappedAttributes, mappedDefaultValue);
	}

	public  System.Object VisitParenExpression(System.Management.Automation.Language.ParenExpressionAst parenExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(parenExpressionAst.Extent);

		PipelineBaseAst mappedPipeline = _VisitPipelineBase(parenExpressionAst.Pipeline);

		return new ParenExpressionAst(mappedExtent, mappedPipeline);
	}

	public  System.Object VisitPipeline(System.Management.Automation.Language.PipelineAst pipelineAst) {
		IScriptExtent mappedExtent = MapExtent(pipelineAst.Extent);

		LinkedList<CommandBaseAst> mappedPipelineElements = new LinkedList<CommandBaseAst>();
		foreach(CommandBaseAst cb in pipelineAst.PipelineElements) {
			mappedPipelineElements.AddLast(_VisitCommandBase(cb));
		}

		return new PipelineAst(mappedExtent, mappedPipelineElements);
	}

	public CommandBaseAst _VisitCommandBase(CommandBaseAst commandBaseAst) {
		var @switch = new Dictionary<Type, Func<CommandBaseAst,CommandBaseAst>> {
			{ typeof(CommandAst), (cb) => { return (CommandBaseAst)VisitCommand(cb as CommandAst);} },
			{ typeof(CommandExpressionAst), (cb) => { return (CommandBaseAst)VisitCommandExpression(cb as CommandExpressionAst);} }
		};

		return @switch[commandBaseAst.GetType()](commandBaseAst);
	}

	public  System.Object VisitReturnStatement(System.Management.Automation.Language.ReturnStatementAst returnStatementAst) {
		IScriptExtent mappedExtent = MapExtent(returnStatementAst.Extent);

		PipelineBaseAst mappedPipeline = returnStatementAst.Pipeline != null ? _VisitPipelineBase(returnStatementAst.Pipeline) : null;

		return new ReturnStatementAst(mappedExtent, mappedPipeline);
	}

	public  System.Object VisitScriptBlock(System.Management.Automation.Language.ScriptBlockAst scriptBlockAst) {
		IScriptExtent mappedExtent = MapExtent(scriptBlockAst.Extent);

		ParamBlockAst mappedParamBlock = scriptBlockAst.ParamBlock == null ? null : (ParamBlockAst)VisitParamBlock(scriptBlockAst.ParamBlock);

		NamedBlockAst mappedBeginBlock = scriptBlockAst.BeginBlock == null ? null : (NamedBlockAst)VisitNamedBlock(scriptBlockAst.BeginBlock);
		NamedBlockAst mappedProcessBlock = scriptBlockAst.ProcessBlock == null ? null : (NamedBlockAst)VisitNamedBlock(scriptBlockAst.ProcessBlock);
		NamedBlockAst mappedEndBlock = scriptBlockAst.EndBlock == null ? null : (NamedBlockAst)VisitNamedBlock(scriptBlockAst.EndBlock);
		NamedBlockAst mappedDynamicParamBlock = scriptBlockAst.DynamicParamBlock == null ? null : (NamedBlockAst)VisitNamedBlock(scriptBlockAst.DynamicParamBlock);

		return new ScriptBlockAst(mappedExtent, mappedParamBlock, mappedBeginBlock, mappedProcessBlock, mappedEndBlock, mappedDynamicParamBlock);
	}

	public  System.Object VisitScriptBlockExpression(System.Management.Automation.Language.ScriptBlockExpressionAst scriptBlockExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(scriptBlockExpressionAst.Extent);

		ScriptBlockAst mappedScriptBlock = (ScriptBlockAst)VisitScriptBlock(scriptBlockExpressionAst.ScriptBlock);

		return new ScriptBlockExpressionAst(mappedExtent, mappedScriptBlock);
	}

	public  System.Object VisitStatementBlock(System.Management.Automation.Language.StatementBlockAst statementBlockAst) {
		IScriptExtent mappedExtent = MapExtent(statementBlockAst.Extent);

		LinkedList<StatementAst> mappedStatements = new LinkedList<StatementAst>();
		foreach(StatementAst s in statementBlockAst.Statements) {
			mappedStatements.AddLast(_VisitStatement(s));
		}
		LinkedList<TrapStatementAst> mappedTraps = new LinkedList<TrapStatementAst>();
		if(statementBlockAst.Traps != null) {
			foreach(TrapStatementAst ts in statementBlockAst.Traps) {
				mappedTraps.AddLast((TrapStatementAst)VisitTrap(ts));
			}
		}

		return new StatementBlockAst(mappedExtent, mappedStatements, mappedTraps);
	}

	public  System.Object VisitStringConstantExpression(System.Management.Automation.Language.StringConstantExpressionAst stringConstantExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(stringConstantExpressionAst.Extent);

		return new StringConstantExpressionAst(mappedExtent, stringConstantExpressionAst.Value, stringConstantExpressionAst.StringConstantType);
	}

	public  System.Object VisitSubExpression(System.Management.Automation.Language.SubExpressionAst subExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(subExpressionAst.Extent);

		StatementBlockAst mappedStatementBlock = (StatementBlockAst)VisitStatementBlock(subExpressionAst.SubExpression);


		return new SubExpressionAst(mappedExtent, mappedStatementBlock);
	}

	public  System.Object VisitSwitchStatement(System.Management.Automation.Language.SwitchStatementAst switchStatementAst) {
		IScriptExtent mappedExtent = MapExtent(switchStatementAst.Extent);

		PipelineBaseAst mappedCondition = _VisitPipelineBase(switchStatementAst.Condition);
		LinkedList<Tuple<ExpressionAst, StatementBlockAst>> mappedClauses = new LinkedList<Tuple<ExpressionAst, StatementBlockAst>>();
		foreach(Tuple<ExpressionAst, StatementBlockAst> c in switchStatementAst.Clauses) {
			mappedClauses.AddLast(
				new Tuple<ExpressionAst, StatementBlockAst>(
					_VisitExpression(c.Item1),
					(StatementBlockAst)VisitStatementBlock(c.Item2)
				)
			);
		}
		StatementBlockAst mappedDefault = switchStatementAst.Default == null ? null : (StatementBlockAst)VisitStatementBlock(switchStatementAst.Default);

		return new SwitchStatementAst(mappedExtent, switchStatementAst.Label, mappedCondition, switchStatementAst.Flags, mappedClauses, mappedDefault);
	}

	public  System.Object VisitThrowStatement(System.Management.Automation.Language.ThrowStatementAst throwStatementAst) {
		IScriptExtent mappedExtent = MapExtent(throwStatementAst.Extent);

		PipelineBaseAst mappedPipeline = _VisitPipelineBase(throwStatementAst.Pipeline);

		return new ThrowStatementAst(mappedExtent, mappedPipeline);
	}

	public  System.Object VisitTrap(System.Management.Automation.Language.TrapStatementAst trapStatementAst) {
		IScriptExtent mappedExtent = MapExtent(trapStatementAst.Extent);

		TypeConstraintAst mappedTrapType = (TypeConstraintAst)VisitTypeConstraint(trapStatementAst.TrapType);
		StatementBlockAst mappedBody = (StatementBlockAst)VisitStatementBlock(trapStatementAst.Body);

		return new TrapStatementAst(mappedExtent, mappedTrapType, mappedBody);
	}

	public  System.Object VisitTryStatement(System.Management.Automation.Language.TryStatementAst tryStatementAst) {
		IScriptExtent mappedExtent = MapExtent(tryStatementAst.Extent);

		StatementBlockAst mappedBody = (StatementBlockAst)VisitStatementBlock(tryStatementAst.Body);
		LinkedList<CatchClauseAst> mappedCatchClauses = new LinkedList<CatchClauseAst>();
		foreach(CatchClauseAst cc in tryStatementAst.CatchClauses) {
			mappedCatchClauses.AddLast((CatchClauseAst)VisitCatchClause(cc));
		}
		StatementBlockAst mappedFinally = tryStatementAst.Finally != null ? (StatementBlockAst)VisitStatementBlock(tryStatementAst.Finally) : null;

		return new TryStatementAst(mappedExtent, mappedBody, mappedCatchClauses, mappedFinally);
	}

	public  System.Object VisitTypeConstraint(System.Management.Automation.Language.TypeConstraintAst typeConstraintAst) {
		IScriptExtent mappedExtent = MapExtent(typeConstraintAst.Extent);

		return new TypeConstraintAst(mappedExtent, typeConstraintAst.TypeName);
	}

	public  System.Object VisitTypeExpression(System.Management.Automation.Language.TypeExpressionAst typeExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(typeExpressionAst.Extent);

		return new TypeExpressionAst(mappedExtent, typeExpressionAst.TypeName);
	}

	public  System.Object VisitUnaryExpression(System.Management.Automation.Language.UnaryExpressionAst unaryExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(unaryExpressionAst.Extent);

		ExpressionAst mappedChild = _VisitExpression(unaryExpressionAst.Child);

		return new UnaryExpressionAst(mappedExtent, unaryExpressionAst.TokenKind, mappedChild);
	}

	public  System.Object VisitUsingExpression(System.Management.Automation.Language.UsingExpressionAst usingExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(usingExpressionAst.Extent);

		ExpressionAst mappedExpressionAst = _VisitExpression(usingExpressionAst.SubExpression);

		return new UsingExpressionAst(mappedExtent, mappedExpressionAst);
	}

	public  System.Object VisitVariableExpression(System.Management.Automation.Language.VariableExpressionAst variableExpressionAst) {
		IScriptExtent mappedExtent = MapExtent(variableExpressionAst.Extent);

		return new VariableExpressionAst(mappedExtent, variableExpressionAst.VariablePath, variableExpressionAst.Splatted);
	}

	public  System.Object VisitWhileStatement(System.Management.Automation.Language.WhileStatementAst whileStatementAst) {
		IScriptExtent mappedExtent = MapExtent(whileStatementAst.Extent);

		PipelineBaseAst mappedCondition = _VisitPipelineBase(whileStatementAst.Condition);
		StatementBlockAst mappedBody = (StatementBlockAst)VisitStatementBlock(whileStatementAst.Body);

		return new WhileStatementAst(mappedExtent, whileStatementAst.Label, mappedCondition, mappedBody);
	}
}