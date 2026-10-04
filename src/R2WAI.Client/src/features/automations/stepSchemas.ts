import type { JsonSchema } from '../../components/SchemaForm'
import type { StepType } from './types'

/**
 * Shared by the Dynamic Step Editor (Phase 7) and the Advanced Automation
 * Builder's node properties panel (Phase 8) — one schema set per real
 * executable step type, matching R2WAI.Api.Workflows.StepActivityFactory's
 * StepConfigDto exactly (the fields each activity actually reads at
 * execution time). Keeping this in one place means both surfaces stay
 * interoperable: a workflow edited in either produces the same Steps JSON
 * shape the other understands.
 */
export const STEP_CONFIG_SCHEMAS: Record<StepType, JsonSchema> = {
  Action: { type: 'object', properties: {} },
  Approval: { type: 'object', properties: {} },
  'AI Generate': {
    type: 'object',
    properties: {
      aiPrompt: { type: 'string', title: 'Prompt', format: 'textarea', description: 'What should the model do?' },
      aiMaxTokens: { type: 'number', title: 'Max tokens' },
      aiTemperature: { type: 'number', title: 'Temperature' },
    },
  },
  Email: {
    type: 'object',
    properties: {
      emailTo: { type: 'string', title: 'To', description: 'Comma-separated addresses' },
      emailCc: { type: 'string', title: 'Cc' },
      emailSubject: { type: 'string', title: 'Subject' },
      emailBody: { type: 'string', title: 'Body', format: 'textarea' },
    },
  },
  'API Call': {
    type: 'object',
    properties: {
      apiUrl: { type: 'string', title: 'URL' },
      apiMethod: { type: 'string', title: 'Method', enum: ['GET', 'POST', 'PUT', 'DELETE', 'PATCH'] },
      apiBody: { type: 'string', title: 'Body', format: 'textarea' },
      apiHeaders: { type: 'string', title: 'Headers (JSON)', format: 'textarea', description: '{"Authorization":"Bearer ..."}' },
    },
  },
  Delay: {
    type: 'object',
    properties: {
      delayDuration: { type: 'number', title: 'Duration' },
      delayUnit: { type: 'string', title: 'Unit', enum: ['minutes', 'hours', 'days'] },
    },
  },
  Condition: {
    type: 'object',
    properties: {
      conditionExpression: {
        type: 'string',
        title: 'Expression',
        description: 'e.g. amount > 1000 — evaluated against the run\'s input data',
      },
    },
  },
  Transform: {
    type: 'object',
    properties: {
      transformInput: { type: 'string', title: 'Input' },
      transformOperation: { type: 'string', title: 'Operation' },
      transformExpression: { type: 'string', title: 'Expression' },
      transformOutput: { type: 'string', title: 'Output variable name' },
    },
  },
}
