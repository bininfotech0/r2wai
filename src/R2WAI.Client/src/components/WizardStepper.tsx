import { Box, Step, StepLabel, Stepper, Typography } from '@mui/material'

export interface WizardStepDefinition {
  /** Stable key. Never localised, never reordered based on display copy. */
  key: string
  label: string
  /** One-line explanation rendered under the active step's label. */
  description?: string
}

interface WizardStepperProps {
  steps: WizardStepDefinition[]
  /** Zero-based index of the step the user is on. */
  activeStep: number
  /**
   * When false the user cannot click ahead to a step they haven't reached. Defaults to
   * false deliberately: a wizard whose steps silently do nothing on click is worse than
   * one that does not look clickable.
   */
  allowStepNavigation?: boolean
  onStepClick?: (index: number) => void
  orientation?: 'horizontal' | 'vertical'
}

/**
 * Progress rail for the multi-step flows (guided agent creation, channel deployment).
 * The same component backs both the horizontal rail on desktop and the vertical rail on
 * narrow viewports, so step numbering and completion read identically across breakpoints.
 */
export function WizardStepper({
  steps,
  activeStep,
  allowStepNavigation = false,
  onStepClick,
  orientation = 'horizontal',
}: WizardStepperProps) {
  const active = steps[activeStep]

  return (
    <Box>
      <Stepper
        activeStep={activeStep}
        orientation={orientation}
        alternativeLabel={orientation === 'vertical'}
        nonLinear={allowStepNavigation}
      >
        {steps.map((step, index) => {
          const reachable = allowStepNavigation && index <= activeStep
          return (
            <Step key={step.key} completed={index < activeStep}>
              <StepLabel
                onClick={
                  allowStepNavigation && onStepClick && index !== activeStep
                    ? () => onStepClick(index)
                    : undefined
                }
                sx={{
                  cursor: reachable && onStepClick ? 'pointer' : 'default',
                  '& .MuiStepLabel-label': { fontWeight: index === activeStep ? 600 : 400 },
                }}
              >
                {step.label}
              </StepLabel>
            </Step>
          )
        })}
      </Stepper>
      {active?.description && (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
          {active.description}
        </Typography>
      )}
    </Box>
  )
}
