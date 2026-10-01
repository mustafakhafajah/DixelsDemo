import * as React from 'react'
import * as SelectPrimitive from '@radix-ui/react-select'
import { CheckIcon, ChevronDownIcon, ChevronUpIcon } from 'lucide-react'

import { cn } from '@/lib/utils'

function Select(props: React.ComponentProps<typeof SelectPrimitive.Root>) {
  return <SelectPrimitive.Root data-slot="select" {...props} />
}

function SelectGroup(props: React.ComponentProps<typeof SelectPrimitive.Group>) {
  return <SelectPrimitive.Group data-slot="select-group" {...props} />
}

function SelectValue(props: React.ComponentProps<typeof SelectPrimitive.Value>) {
  return <SelectPrimitive.Value data-slot="select-value" {...props} />
}

function SelectTrigger({ className, size = 'default', children, ...props }: React.ComponentProps<typeof SelectPrimitive.Trigger> & {
  size?: 'sm' | 'default'
}) {
  return (
    <SelectPrimitive.Trigger
      data-slot="select-trigger"
      data-size={size}
      className={cn(
        'tw:flex tw:w-full tw:items-center tw:justify-between tw:gap-2 tw:rounded-[9px] tw:border tw:border-solid tw:border-input tw:bg-background tw:px-3 tw:text-start tw:font-[inherit] tw:text-sm tw:text-foreground tw:whitespace-nowrap tw:shadow-[0_1px_2px_rgba(24,24,27,.04)] tw:outline-none tw:cursor-pointer',
        'tw:data-[size=default]:h-[39px] tw:data-[size=sm]:h-[30px] tw:data-[size=sm]:rounded-lg tw:data-[size=sm]:px-2 tw:data-[size=sm]:text-xs',
        'tw:focus-visible:border-ring tw:focus-visible:outline-3 tw:focus-visible:outline-accent tw:data-[state=open]:border-ring',
        'tw:data-[placeholder]:text-muted-foreground tw:disabled:cursor-not-allowed tw:disabled:opacity-50 tw:disabled:bg-muted',
        'tw:*:data-[slot=select-value]:line-clamp-1 tw:*:data-[slot=select-value]:flex tw:*:data-[slot=select-value]:items-center tw:*:data-[slot=select-value]:gap-2',
        "tw:[&_svg]:pointer-events-none tw:[&_svg]:shrink-0 tw:[&_svg:not([class*='size-'])]:size-4",
        className,
      )}
      {...props}
    >
      {children}
      <SelectPrimitive.Icon asChild>
        <ChevronDownIcon className="tw:size-4 tw:opacity-60" />
      </SelectPrimitive.Icon>
    </SelectPrimitive.Trigger>
  )
}

function SelectContent({ className, children, position = 'popper', ...props }: React.ComponentProps<typeof SelectPrimitive.Content>) {
  return (
    <SelectPrimitive.Portal>
      <SelectPrimitive.Content
        data-slot="select-content"
        className={cn(
          'tw:relative tw:z-[80] tw:max-h-(--radix-select-content-available-height) tw:min-w-[8rem] tw:origin-(--radix-select-content-transform-origin) tw:overflow-x-hidden tw:overflow-y-auto tw:rounded-[10px] tw:border tw:border-solid tw:border-border tw:bg-popover tw:font-sans tw:text-sm tw:text-popover-foreground tw:shadow-[0_8px_24px_rgba(0,0,0,.12)]',
          position === 'popper' && 'tw:data-[side=bottom]:translate-y-1 tw:data-[side=top]:-translate-y-1',
          className,
        )}
        position={position}
        {...props}
      >
        <SelectScrollUpButton />
        <SelectPrimitive.Viewport
          className={cn('tw:p-1', position === 'popper' && 'tw:h-[var(--radix-select-trigger-height)] tw:w-full tw:min-w-[var(--radix-select-trigger-width)] tw:scroll-my-1')}
        >
          {children}
        </SelectPrimitive.Viewport>
        <SelectScrollDownButton />
      </SelectPrimitive.Content>
    </SelectPrimitive.Portal>
  )
}

function SelectLabel({ className, ...props }: React.ComponentProps<typeof SelectPrimitive.Label>) {
  return <SelectPrimitive.Label data-slot="select-label" className={cn('tw:px-2 tw:py-1.5 tw:text-xs tw:text-muted-foreground', className)} {...props} />
}

function SelectItem({ className, children, ...props }: React.ComponentProps<typeof SelectPrimitive.Item>) {
  return (
    <SelectPrimitive.Item
      data-slot="select-item"
      className={cn(
        'tw:relative tw:flex tw:w-full tw:cursor-default tw:items-center tw:gap-2 tw:rounded-md tw:py-1.5 tw:pe-8 tw:ps-2 tw:text-sm tw:outline-none tw:select-none',
        'tw:focus:bg-accent tw:focus:text-accent-foreground tw:data-[state=checked]:font-semibold tw:data-[disabled]:pointer-events-none tw:data-[disabled]:opacity-50',
        "tw:[&_svg]:pointer-events-none tw:[&_svg]:shrink-0 tw:[&_svg:not([class*='size-'])]:size-4",
        className,
      )}
      {...props}
    >
      <span className="tw:absolute tw:end-2 tw:flex tw:size-3.5 tw:items-center tw:justify-center">
        <SelectPrimitive.ItemIndicator>
          <CheckIcon className="tw:size-4 tw:text-primary" />
        </SelectPrimitive.ItemIndicator>
      </span>
      <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
    </SelectPrimitive.Item>
  )
}

function SelectSeparator({ className, ...props }: React.ComponentProps<typeof SelectPrimitive.Separator>) {
  return <SelectPrimitive.Separator data-slot="select-separator" className={cn('tw:pointer-events-none tw:-mx-1 tw:my-1 tw:h-px tw:bg-border', className)} {...props} />
}

function SelectScrollUpButton({ className, ...props }: React.ComponentProps<typeof SelectPrimitive.ScrollUpButton>) {
  return (
    <SelectPrimitive.ScrollUpButton data-slot="select-scroll-up-button" className={cn('tw:flex tw:cursor-default tw:items-center tw:justify-center tw:py-1', className)} {...props}>
      <ChevronUpIcon className="tw:size-4" />
    </SelectPrimitive.ScrollUpButton>
  )
}

function SelectScrollDownButton({ className, ...props }: React.ComponentProps<typeof SelectPrimitive.ScrollDownButton>) {
  return (
    <SelectPrimitive.ScrollDownButton data-slot="select-scroll-down-button" className={cn('tw:flex tw:cursor-default tw:items-center tw:justify-center tw:py-1', className)} {...props}>
      <ChevronDownIcon className="tw:size-4" />
    </SelectPrimitive.ScrollDownButton>
  )
}

export { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectScrollDownButton, SelectScrollUpButton, SelectSeparator, SelectTrigger, SelectValue }
